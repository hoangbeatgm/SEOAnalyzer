using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;

namespace SEOAnalyzer
{
    public class GoogleAnalyticsService
    {
        private readonly string _tokenPath;
        private readonly string _propertyIdPath;
        private readonly string _propertyId;

        public GoogleAnalyticsService(string propertyIdPath, string credentialsPath)
        {
            _tokenPath = credentialsPath;
            _propertyIdPath = propertyIdPath;
            if (!File.Exists(_propertyIdPath))
                MessageBox.Show("Không tìm thấy file Analytics_propertyId.txt!", _propertyIdPath);

            _propertyId = File.ReadAllText(_propertyIdPath).Trim();
            
        }

        private async Task<string> GetAuthTokenAsync()
        {
            var scopes = new[] { "https://www.googleapis.com/auth/analytics.readonly" };

            using (var stream = new FileStream(_tokenPath, FileMode.Open, FileAccess.Read))
            {
                var credential = GoogleCredential.FromStream(stream).CreateScoped(scopes);

                if (credential.UnderlyingCredential is ServiceAccountCredential serviceCred)
                {
                    var token = await serviceCred.GetAccessTokenForRequestAsync();
                    return $"Bearer {token}";
                }
            }

            throw new Exception("Không thể lấy token từ credentials.json");
        }


        public async Task<AnalyticsData> GetAnalyticsData(string dateRange)
        {
            var dates = GetDateForRange(dateRange);
            var authToken = (await GetAuthTokenAsync()).Replace("Bearer ", "");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken.Replace("Bearer ", ""));

                var request = new
                {
                    dateRanges = new[] { new { startDate = dates.startDate, endDate = dates.endDate } },
                    dimensions = new[] { new { name = "date" } },
                    metrics = new[]
                    {
                new { name = "newUsers" },
                new { name = "activeUsers" },
                new { name = "sessions" },
                new { name = "eventCount" },
                new { name = "screenPageViews" },
                new { name = "eventCountPerUser" },
                new { name = "sessionsPerUser" },
                new { name = "eventsPerSession" },
                new { name = "bounceRate" },
                new { name = "engagementRate" },
            }
                };

                var response = await client.PostAsJsonAsync(
                    $"https://analyticsdata.googleapis.com/v1beta/properties/{_propertyId}:runReport",
                    request);

                response.EnsureSuccessStatusCode();

                var responseData = await response.Content.ReadAsAsync<dynamic>();
                var rows = responseData.rows ?? new List<dynamic>();

                var data = new List<AnalyticsDataItem>();
                foreach (var row in rows)
                {
                    try
                    {
                        data.Add(new AnalyticsDataItem
                        {
                            Date = row.dimensionValues[0]?.value ?? "Unknown",
                            NewUsers = int.TryParse(row.metricValues[0]?.value?.ToString(), out int newUsers) ? newUsers : 0,
                            ActiveUsers = int.TryParse(row.metricValues[1]?.value?.ToString(), out int activeUsers) ? activeUsers : 0,
                            Sessions = int.TryParse(row.metricValues[2]?.value?.ToString(), out int sessions) ? sessions : 0,
                            EventCount = int.TryParse(row.metricValues[3]?.value?.ToString(), out int eventCount) ? eventCount : 0,
                            ScreenPageViews = int.TryParse(row.metricValues[4]?.value?.ToString(), out int screenPageViews) ? screenPageViews : 0,
                            EventsPerUser = float.TryParse(row.metricValues[5]?.value?.ToString(), out float eventsPerUser) ? eventsPerUser : 0f,
                            SessionsPerUser = float.TryParse(row.metricValues[6]?.value?.ToString(), out float sessionsPerUser) ? sessionsPerUser : 0f,
                            EventsPerSession = float.TryParse(row.metricValues[7]?.value?.ToString(), out float eventsPerSession) ? eventsPerSession : 0f,
                            BounceRate = float.TryParse(row.metricValues[8]?.value?.ToString(), out float bounceRate) ? bounceRate : 0f,
                            EngagementRate = float.TryParse(row.metricValues[9]?.value?.ToString(), out float engagementRate) ? engagementRate : 0f,
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi khi xử lý dòng dữ liệu: {ex.Message}");
                    }
                }

                // Sắp xếp dữ liệu chính theo ngày tăng dần
                data.Sort((a, b) => a.Date.CompareTo(b.Date));

                return new AnalyticsData
                {
                    Items = data,
                };
            }
        }
        public async Task<List<PageTitleAnalyticsItem>> GetPageTitleAnalyticsAsync(string dateRange)
        {
            var dates = GetDateForRange(dateRange);
            var authToken = (await GetAuthTokenAsync()).Replace("Bearer ", "");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken.Replace("Bearer ", ""));

                var request = new
                {
                    dateRanges = new[] { new { startDate = dates.startDate, endDate = dates.endDate } },
                    dimensions = new[] { new { name = "pageTitle" } },
                    metrics = new[]
                    {
                new { name = "screenPageViews" },
                new { name = "activeUsers" },
                new { name = "screenPageViewsPerUser" },
                new { name = "userEngagementDuration" },
                new { name = "eventCount" },
                    },
                    limit = 1000
                };

                var response = await client.PostAsJsonAsync(
                    $"https://analyticsdata.googleapis.com/v1beta/properties/{_propertyId}:runReport",
                    request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Lỗi Analytics API: {response.StatusCode} - {errorContent}");
                    throw new Exception($"Lỗi Analytics API: {response.StatusCode} - {errorContent}");
                }
                response.EnsureSuccessStatusCode();

                var responseData = await response.Content.ReadAsAsync<dynamic>();
                var rows = responseData.rows ?? new List<dynamic>();

                var result = new List<PageTitleAnalyticsItem>();
                foreach (var row in rows)
                {
                    try
                    {
                        result.Add(new PageTitleAnalyticsItem
                        {
                            PageTitle = row.dimensionValues[0]?.value ?? "Unknown",
                            ScreenPageViews = int.TryParse(row.metricValues[0]?.value?.ToString(), out int screenpageviews) ? screenpageviews : 0,
                            ActiveUsers = int.TryParse(row.metricValues[1]?.value?.ToString(), out int activeusers) ? activeusers : 0,
                            ViewsPerUser = float.TryParse(row.metricValues[2]?.value?.ToString(), out float viewsperuser) ? viewsperuser : 0f,
                            UserEngagementDuration = float.TryParse(row.metricValues[3]?.value?.ToString(), out float userengagementduration) ? userengagementduration : 0f,
                            EventCount = int.TryParse(row.metricValues[4]?.value?.ToString(), out int v5) ? v5 : 0
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi khi xử lý dòng dữ liệu pageTitle: {ex.Message}");
                    }
                }
                return result;
            }

        }
        public async Task<List<CityActiveUserItem>> GetActiveUsersByCityAsync(string dateRange)
        {
            var dates = GetDateForRange(dateRange);
            var authToken = (await GetAuthTokenAsync()).Replace("Bearer ", "");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var request = new
                {
                    dateRanges = new[] { new { startDate = dates.startDate, endDate = dates.endDate } },
                    dimensions = new[] { new { name = "city" } },
                    metrics = new[] { new { name = "activeUsers" } },
                    limit = 20 // Giới hạn số dòng kết quả, tùy ý
                };

                var response = await client.PostAsJsonAsync(
                    $"https://analyticsdata.googleapis.com/v1beta/properties/{_propertyId}:runReport",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Lỗi Analytics API: {response.StatusCode} - {errorContent}");
                    throw new Exception($"Lỗi Analytics API: {response.StatusCode} - {errorContent}");
                }

                var responseData = await response.Content.ReadAsAsync<dynamic>();
                var rows = responseData.rows ?? new List<dynamic>();

                var result = new List<CityActiveUserItem>();
                foreach (var row in rows)
                {
                    try
                    {
                        result.Add(new CityActiveUserItem
                        {
                            City = row.dimensionValues[0]?.value ?? "Unknown",
                            ActiveUsers = int.TryParse(row.metricValues[0]?.value?.ToString(), out int users) ? users : 0
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi khi xử lý dữ liệu thành phố: {ex.Message}");
                    }
                }

                return result;
            }
        }

        private (string startDate, string endDate) GetDateForRange(string daterange)
        {
            var today = System.DateTime.Today;
            System.DateTime startDate;
            System.DateTime endDate;

            switch (daterange)
            {
                case "today":
                    startDate = endDate = today;
                    break;

                case "yesterday":
                    startDate = endDate = today.AddDays(-1);
                    break;

                case "this_week":
                    // lấy ngày hôm nay
                    //Sunday = 0 Monday = 1 Tuesday = 2 Wednesday = 3 Thursday = 4
                    int dayOfWeekThis = (int)today.DayOfWeek;

                    if (dayOfWeekThis == 0) // Nếu là Chủ Nhật (DayOfWeek = 0)
                    {
                        endDate = today.AddDays(-1); // Hôm qua
                        startDate = endDate.AddDays(-6); // 6 ngày trước hôm qua
                    }
                    else
                    {
                        // Chủ Nhật của tuần hiện tại
                        System.DateTime firstDayOfWeek = today.AddDays(-dayOfWeekThis); // Chủ Nhật
                        System.DateTime yesterday = today.AddDays(-1); // Hôm qua
                        startDate = firstDayOfWeek;
                        endDate = yesterday;
                    }
                    break;

                case "last_week":
                    int dayOfWeek = (int)today.DayOfWeek;

                    if (dayOfWeek == 0) // Nếu hôm nay là Chủ Nhật
                    {
                        endDate = today;
                        startDate = endDate.AddDays(-6);
                    }
                    else
                    {
                        System.DateTime startOfThisWeek = today.AddDays(-dayOfWeek + 1);
                        System.DateTime startOfLastWeek = startOfThisWeek.AddDays(-7);
                        System.DateTime endOfLastWeek = startOfThisWeek.AddDays(-1);

                        startDate = startOfLastWeek;
                        endDate = endOfLastWeek;
                    }
                    break;

                case "last_7_days":
                    endDate = today.AddDays(-1);
                    startDate = endDate.AddDays(-6);
                    break;
                case "last_14_days":
                    endDate = today.AddDays(-1);
                    startDate = endDate.AddDays(-13);
                    break;
                default:
                    throw new ArgumentException("dateRange không hợp lệ.");
            }

            return (startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
        }
    }

    public class AnalyticsData
    {
        public List<AnalyticsDataItem> Items { get; set; }
    }

    public class AnalyticsDataItem
    {
        public string Date { get; set; }
        public int NewUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int Sessions { get; set; }
        public int EventCount { get; set; }
        public int ScreenPageViews { get; set; }
        public float EventsPerUser { get; set; }
        public float SessionsPerUser { get; set; }
        public float EventsPerSession { get; set; }
        public float BounceRate { get; set; }
        public float EngagementRate { get; set; }
    }
    public class PageTitleAnalyticsItem
    {
        public string PageTitle { get; set; }
        public int ScreenPageViews { get; set; }
        public int ActiveUsers { get; set; }
        public float ViewsPerUser { get; set; }
        public float UserEngagementDuration { get; set; }
        public int EventCount { get; set; }
    }
    public class CityActiveUserItem
    {
        public string City { get; set; }
        public int ActiveUsers { get; set; }
    }
}