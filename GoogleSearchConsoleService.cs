using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Google.Apis.Auth.OAuth2; // su dung package de xac thuc
using Microsoft.VisualBasic.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SEOAnalyzer
{
    public class GoogleSearchConsoleService
    {
        private readonly string _credentialsFilePath;
        private readonly string _siteUrl; // Site của bạn

        public GoogleSearchConsoleService(string credentialsFilePath, string SearchConsole_SiteURL)
        {
            _credentialsFilePath = credentialsFilePath;
            _siteUrl = SearchConsole_SiteURL;
            if (!File.Exists(_siteUrl))
                MessageBox.Show("Không tìm thấy file propertyId.txt", _siteUrl);
            _siteUrl = File.ReadAllText(_siteUrl).Trim(); // Đọc site URL từ file
        }

        private async Task<string> GetAuthTokenAsync()
        {
            GoogleCredential credential;

            using (var stream = new FileStream(_credentialsFilePath, FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleCredential
                    .FromStreamAsync(stream, CancellationToken.None);
                credential = credential.CreateScoped("https://www.googleapis.com/auth/webmasters.readonly");
            }

            var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            return token;
        }
        public async Task<List<DailyPerformanceData>> GetDailyPerformanceData(string startDate, string endDate)
        {
            var authToken = await GetAuthTokenAsync();
           
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken.Replace("Bearer ", ""));

                var request = new
                {
                    startDate= startDate,
                    endDate= endDate,
                    dimensions = new[] { "date" },
                    rowLimit = 1000
                };
                var response = await client.PostAsJsonAsync(
                    $"https://searchconsole.googleapis.com/webmasters/v3/sites/{Uri.EscapeDataString(_siteUrl)}/searchAnalytics/query",
                    request);

                response.EnsureSuccessStatusCode();

                var responseData = await response.Content.ReadAsAsync<dynamic>();

                var rows = responseData.rows ?? new List<dynamic>();

                var result = new List<DailyPerformanceData>();
                foreach (var row in rows)
                {
                    if (row.keys[0] == null) continue;

                    if (!DateTime.TryParse(row.keys[0].ToString(), out DateTime date))
                        continue;

                    // Ép kiểu Clicks sang int và Impressions sang double
                    result.Add(new DailyPerformanceData
                    {
                        Date = date,
                        Clicks = Convert.ToInt32(row.clicks ?? 0),
                        Impressions = Convert.ToInt32(row.impressions ?? 0),
                        Ctr = row.impressions > 0 ? (float)(row.clicks * 100.0 / row.impressions) : 0,
                        AvgPosition = row.position ?? 0
                    });

                }
                return result;
            }
        }

        public async Task<List<SearchConsoleData>> GetSearchConsoleData(string startDate, string endDate, string typeDate)
        {
            var authToken = await GetAuthTokenAsync();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken.Replace("Bearer ", ""));

                var request = new
                {
                    startDate,
                    endDate,
                    dimensions = new[] { "query", "page" },
                    rowLimit = 5000
                };

                var response = await client.PostAsJsonAsync(
                    $"https://searchconsole.googleapis.com/webmasters/v3/sites/{Uri.EscapeDataString(_siteUrl)}/searchAnalytics/query",
                    request);

                response.EnsureSuccessStatusCode();
                var responseData = await response.Content.ReadAsAsync<dynamic>();
                var rows = responseData.rows ?? new List<dynamic>();

                // Tạo group để tổng hợp dữ liệu từ khóa
                var grouped = new Dictionary<string, SearchConsoleData>();
                var allPages = new HashSet<string>(); // Lưu tất cả các trang không trùng lặp

                foreach (var row in rows)
                {
                    var keyword = row.keys[0]?.ToString() ?? "Không xác định";
                    var page = row.keys[1]?.ToString() ?? "Không xác định";
                    var clicks = (int)(row.clicks ?? 0);
                    var impressions = (int)(row.impressions ?? 0);
                    var position = (float)(row.position ?? 0);
                    var ctr = impressions > 0 ? (clicks * 100f / impressions) : 0;
                    if (!grouped.ContainsKey(keyword))
                    {
                        grouped[keyword] = new SearchConsoleData
                        {
                            Keyword = keyword,
                            Clicks = clicks,
                            Impressions = impressions,
                            PositionSum = position, 
                            Ctr = ctr,
                            Count = 1,
                            Pages = new HashSet<string> { page }
                        };
                    }
                    else
                    {
                        grouped[keyword].Clicks = Math.Max(grouped[keyword].Clicks, clicks);
                        if (ctr > grouped[keyword].Ctr)
                        {
                            grouped[keyword].PositionSum = position;
                        }
                        else if (ctr == 0 && impressions > grouped[keyword].Impressions)
                        {

                            grouped[keyword].PositionSum = position;
                        }
                        grouped[keyword].Impressions = Math.Max(grouped[keyword].Impressions, impressions);
                        grouped[keyword].Ctr = Math.Max(grouped[keyword].Ctr, ctr);
                        grouped[keyword].Count++;
                        grouped[keyword].Pages.Add(page);
                    }

                    allPages.Add(page);
                }

                // Tính tỷ lệ phần trăm sử dụng của từng từ khóa
                var result = new List<SearchConsoleData>();
                var count = 0;
                foreach (var item in grouped.Values)
                {
                    count += (int)item.Pages.Count;
                }
                foreach (var item in grouped.Values)
                {
                    //UsagePercentage = (Tổng số lần xuất hiện của một từ khóa trên tất cả các trang/ Tổng số lần xuất hiện của tất cả từ khóa trên các trang) * 100
                    float solanxuathien = ((float)item.Pages.Count / (float)count) * 100;
                    result.Add(new SearchConsoleData
                    {
                        Keyword = item.Keyword,
                        Clicks = item.Clicks,
                        Impressions = item.Impressions,
                        Ctr = item.Impressions > 0 ? (item.Clicks * 100f / item.Impressions) : 0,
                        AvgPosition = item.PositionSum,
                        OldAvgPosition = readFile(item.Keyword, typeDate),
                        PageCount = item.Pages.Count, // Số lượng trang mà từ khóa xuất hiện
                        UsagePercentage = solanxuathien// Tỷ lệ phần trăm sử dụng
                    });
                }
                result.Sort((a, b) =>
                {
                    if (b.Clicks == a.Clicks)
                        return b.Impressions - a.Impressions;
                    return b.Clicks - a.Clicks;
                });
                return result;
            }
        }


        //hàm đọc file vị trí cũ
        private float readFile(string keyWord, string typeDate)
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{typeDate}.txt");
            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath); 

                foreach (string line in lines)
                {
                    string kw = line.Split("==")[0];
                    string pos = line.Split("==")[1];
                    if (keyWord == kw)
                    {
                        return float.Parse(pos);
                    }
                }
            }
            else
            {
                Console.WriteLine($"File không tồn tại: {filePath}");
            }
            return 0;
        }
        private async Task<int> GetKeywordCountForPage(string pageUrl, string startDate, string endDate)
        {
            var authToken = await GetAuthTokenAsync();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken.Replace("Bearer ", ""));

                var request = new
                {
                    startDate,
                    endDate,
                    dimensions = new[] { "query" },
                    dimensionFilterGroups = new[]
                    {
                        new
                        {
                            filters = new[]
                            {
                                new
                                {
                                    dimension = "page",
                                    operator_ = "equals",
                                    expression = pageUrl
                                }
                            }
                        }
                    },
                    rowLimit = 1000
                };

                var response = await client.PostAsJsonAsync(
                    $"https://searchconsole.googleapis.com/webmasters/v3/sites/{Uri.EscapeDataString(_siteUrl)}/searchAnalytics/query",
                    request);

                response.EnsureSuccessStatusCode();

                var responseData = await response.Content.ReadAsAsync<dynamic>();
                var rows = responseData.rows as IEnumerable<dynamic> ?? Enumerable.Empty<dynamic>();

                // Đếm số lượng từ khóa
                return rows.Count();
            }
        }
        public async Task<List<PagePerformanceData>> GetPagePerformanceData(string startDate, string endDate)
        {
            var authToken = await GetAuthTokenAsync();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken.Replace("Bearer ", ""));

                var request = new
                {
                    startDate,
                    endDate,
                    dimensions = new[] { "page" },
                    rowLimit = 2000
                };

                var response = await client.PostAsJsonAsync(
                    $"https://searchconsole.googleapis.com/webmasters/v3/sites/{Uri.EscapeDataString(_siteUrl)}/searchAnalytics/query",
                    request);

                response.EnsureSuccessStatusCode();

                var responseData = await response.Content.ReadAsAsync<dynamic>();
                var rows = responseData.rows ?? new List<dynamic>();

                var result = new List<PagePerformanceData>();
                foreach (var row in rows)
                {
                    var page = row.keys[0]?.ToString() ?? "Unknown";
                    var clicks = Convert.ToInt32(row.clicks ?? 0);
                    var impressions = Convert.ToInt32(row.impressions ?? 0);
                    var ctr = impressions > 0 ? (float)(row.clicks * 100f / row.impressions) : 0;
                    var avgPosition = (float)(row.position ?? 0);

                    // Lấy số lượng từ khóa cho trang này
                    int keywordsCount = await GetKeywordCountForPage(page, startDate, endDate);

                    result.Add(new PagePerformanceData
                    {
                        Page = page,
                        Clicks = clicks,
                        Impressions = impressions,
                        Ctr = ctr,
                        AvgPosition = avgPosition,
                        StartDate = startDate,
                        EndDate = endDate,
                        KeywordsCount = keywordsCount // Thêm số lượng từ khóa vào kết quả
                    });
                    Debug.WriteLine(result);

                }
                return result;
            }
        }
        public async Task<List<PageKeywordData>> GetKeywordDataForPageAsync(string urlPage, int pageClicks, int pageImpressions, float pageCtr, float pageAvgPosition, string startDate, string endDate)
        {

            try
            {
                var authToken = await GetAuthTokenAsync();
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken?.Replace("Bearer ", ""));

                    var request = new
                    {
                        startDate,
                        endDate,
                        dimensions = new[] { "query", "page" },
                        dimensionFilterGroups = new[]
                        {
                    new
                    {
                        filters = new[]
                        {
                            new
                            {
                                dimension = "page",
                                operator_ = "equals",
                                expression = urlPage
                            }
                        }
                    }
                },
                        rowLimit = 1000
                    };

                    var apiUrl = $"https://searchconsole.googleapis.com/webmasters/v3/sites/{Uri.EscapeDataString(_siteUrl)}/searchAnalytics/query";
                    var response = await client.PostAsJsonAsync(apiUrl, request);
                    response.EnsureSuccessStatusCode();

                    var responseData = await response.Content.ReadAsAsync<dynamic>();
                    var rows = responseData.rows as IEnumerable<dynamic> ?? Enumerable.Empty<dynamic>(); // thử ép kiểu as IEnumerable<dynamic> ?? là toán tử null-coalescing nếu về trái là null thì dùng về phải  Enumerable.Empty<dynamic>();

                    // Log số lượng dòng dữ liệu nhận được
                    Debug.WriteLine($"Số lượng dòng dữ liệu từ API: {rows.Count()}");

                    var result = new List<PageKeywordData>();
                    foreach (var row in rows)
                    {
                        try
                        {
                            var keys = row.keys as IEnumerable<dynamic> ?? Enumerable.Empty<dynamic>();
                            var keyword = keys.ElementAtOrDefault(0)?.ToString() ?? "Không xác định";

                            var clicks = Convert.ToInt32(row.clicks ?? 0);
                            var impressions = Convert.ToInt32(row.impressions ?? 0);
                            var position = Convert.ToSingle(row.position ?? 0f);
                            var ctr = impressions > 0 ? (clicks * 100f / impressions) : 0f;

                            result.Add(new PageKeywordData
                            {
                                Page = urlPage,
                                Keyword = keyword,
                                Clicks = clicks,
                                Impressions = impressions,
                                Ctr = ctr,
                                AvgPosition = position
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Lỗi khi xử lý dòng: {ex.Message}");
                            continue;
                        }
                    }
                    if (!result.Any()) // nếu result rỗng
                    {
                        var replaceResult = new PageKeywordData
                        {
                            Page = urlPage,
                            Keyword = "(Không có từ khoá)",
                            Clicks = 0,
                            Impressions = 0,
                            Ctr = 0,
                            AvgPosition = 0
                        };
                        result.Add(replaceResult);
                    }

                    // Nếu result có dữ liệu mà khác (Không có từ khoá) thì tính tổng số khoá result.count, ngược lại nếu Keyword là "(Không có từ khoá)") thì result.count =0
                    int totalKeywords = result.Any(x => x.Keyword != "(Không có từ khoá)") ? result.Count : 0;

                    foreach (var item in result)
                    {
                        item.CapNhatDanhGia(totalKeywords, pageClicks, pageImpressions, pageCtr, pageAvgPosition);
                    }


                    // Hiển thị MessageBox để xem dữ liệu 
                    MessageBox.Show(
                            $"Dữ liệu trả về:\n" +
                            $"Page: {result.FirstOrDefault()?.Page}\n" +
                            $"Tổng số từ khoá: {totalKeywords}\n" +
                            $"Lượt nhấp: {pageClicks}\n" +
                            $"Lượt hiển thị: {pageImpressions}\n" +
                            $"CTR: {pageCtr} \n" +
                            $"Vị trí trung bình: {pageAvgPosition}\n" +
                            $"Đánh giá: {result.FirstOrDefault()?.DanhGia}\n" +
                            $"Hành động: {result.FirstOrDefault()?.HanhDongDeXuat}\n",
                            "Truy vấn Page",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );

                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi nghiêm trọng: {ex}");
                MessageBox.Show($"Lỗi khi gọi API: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

    }
    public class DailyPerformanceData
    {
        public DateTime Date { get; set; }
        public int Clicks { get; set; }
        public int Impressions { get; set; }
        public float Ctr { get; set; }
        public float AvgPosition { get; set; }
    }

    public class SearchConsoleData
    {
        public string Keyword { get; set; }
        public int Clicks { get; set; }
        public int Impressions { get; set; }
        public float Ctr { get; set; }
        public float AvgPosition { get; set; }
        public float PositionSum { get; set; }
        public int Count { get; set; }
        public float OldAvgPosition { get; set; }
        public HashSet<string> Pages { get; set; } // Danh sách các trang mà từ khóa xuất hiện
        public int PageCount { get; set; } // Tổng số trang mà từ khóa xuất hiện
        public float UsagePercentage { get; set; } // Tỷ lệ phần trăm sử dụng
    }


    public class PagePerformanceData
    {
        public string Page { get; set; }
        public int Clicks { get; set; }
        public int Impressions { get; set; }
        public float Ctr { get; set; }
        public float AvgPosition { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public int KeywordsCount { get; set; }
    }
    public class PageKeywordData
    {
        public string Page { get; set; }
        public string Keyword { get; set; }
        public int Clicks { get; set; }
        public int Impressions { get; set; }
        public float Ctr { get; set; }
        public float AvgPosition { get; set; }
        public string DanhGia { get; set; }
        public string HanhDongDeXuat { get; set; }

        public void CapNhatDanhGia(int totalKeywords, int pageClicks, int pageImpressions, float pageCtr, float pageAvgPosition)
        {
            // Từ khoá ít khi ít hơn 5, vị trí kém khi lớn hơn 8, CTR thấp khi nhỏ hơn 2% 
            if (totalKeywords == 0)  // Kiểm tra không có từ khoá
            {
                DanhGia = "Không có từ khóa";
                HanhDongDeXuat = "Thêm từ khóa vào bài viết và tối ưu lại nội dung trang";
            }
            else if (totalKeywords >= 5 && pageAvgPosition < 8 && pageCtr < 2.0f)
            {
                DanhGia = "Số lượng từ khoá tìm kiếm OK + Vị trí tốt + CTR thấp";
                HanhDongDeXuat = "Tối ưu lại tiêu đề + meta description, thêm thumbnail nếu chưa có";
            }
            else if (totalKeywords >= 5 && pageAvgPosition > 8)
            {
                DanhGia = "Số lượng từ khoá tìm kiếm OK + Vị trí trung bình kém";
                HanhDongDeXuat = "Chỉnh sửa lại nội dung bài viết: heading, nội dung, hình ảnh";
            }
            else if (totalKeywords < 5 && pageAvgPosition > 8)
            {
                DanhGia = "Ít từ khoá và vị trí kém";
                HanhDongDeXuat = "Cân nhắc xoá hoặc viết lại toàn bộ nội dung";
            }
            else
            {
                DanhGia = "Nhìn chung Page có số liệu SEO ổn";
                HanhDongDeXuat = "Tích cực tối ưu hoá Page";
            }
        }
    }
}
