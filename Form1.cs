using System;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Azure.Core;

namespace SEOAnalyzer
{
    public partial class Form1 : Form
    {
        private GoogleAnalyticsService _analyticsService;
        private GoogleSearchConsoleService _searchConsoleService;
        private readonly WebView2 _webView;
        string propertyIdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Analytics_propertyId.txt");
        string credentialsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json");
        string SearchConsole_SiteURL = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SearchConsole_siteURL.txt");
        string _startDate, _endDate, _typeDate, _typeDateConso;
        private List<PagePerformanceData> _pagePerformanceData; // Biến lưu trữ dữ liệu pagePerformance
        public Form1()
        {
            InitializeComponent();

            // Khởi tạo các service với đường dẫn token
            _analyticsService = new GoogleAnalyticsService(propertyIdPath, credentialsPath);
            _searchConsoleService = new GoogleSearchConsoleService(credentialsPath, SearchConsole_SiteURL);
            showPropertyID.Text = "AnalyticsPropertyID: " + File.ReadAllText(propertyIdPath).Trim();
            showUrlsite.Text = "SiteURL: " + File.ReadAllText(SearchConsole_SiteURL).Trim();
            _webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(_webView);

            InitializeWebView();

        }
        public void ReloadData()
        {
            showPropertyID.Text = "AnalyticsPropertyID: " + File.ReadAllText(propertyIdPath).Trim();
            showUrlsite.Text = "SiteURL: " + File.ReadAllText(SearchConsole_SiteURL).Trim();

            // Khởi tạo lại service
            _analyticsService = new GoogleAnalyticsService(propertyIdPath, credentialsPath);
            _searchConsoleService = new GoogleSearchConsoleService(credentialsPath, SearchConsole_SiteURL);
        }
        private async void InitializeWebView()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                string webPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Web", "index.html");

                if (File.Exists(webPath))
                {
                    _webView.Source = new Uri($"file:///{webPath.Replace("\\", "/")}");

                    // Đăng ký callback để nhận message từ JavaScript
                    _webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

                    // Cho phép JavaScript gọi C#
                    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                        "window.chrome = { webview: { postMessage: function(data) { window.chrome.webview.postMessage(data); } } };");
                }
                else
                {

                    MessageBox.Show($"Không tìm thấy file: {webPath}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi WebView2: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string propertyIdContent = File.ReadAllText(propertyIdPath).Trim();
            string siteUrlContent = File.ReadAllText(SearchConsole_SiteURL).Trim();

            if (string.IsNullOrEmpty(propertyIdContent) && string.IsNullOrEmpty(siteUrlContent))
            {
                // Hiển thị thông báo lỗi nếu file trống
                await _webView.ExecuteScriptAsync(@"
                    (function() {
                        let t = document.createElement('div');
                        t.textContent = 'Không thể truy xuất API. Hãy chọn website để phân tích!';
                        Object.assign(t.style, {
                            position: 'fixed', bottom: '40px', left: '50%', transform: 'translateX(-50%)',
                            background: '#e74c3c', color: 'white', padding: '14px 32px', borderRadius: '10px',
                            fontSize: '18px', fontWeight: 'bold', zIndex: '9999', opacity: '0',
                            boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
                            transition: 'opacity 0.5s ease'
                        });
                        document.body.appendChild(t);
                        setTimeout(() => t.style.opacity = '1', 100);
                        setTimeout(() => { t.style.opacity = '0'; setTimeout(() => t.remove(), 500); }, 3000);
                    })();
                ");
                toolStripComboBox.Enabled = true;
            }
            toolStripComboBox.Enabled = false;
            try
            {
                var message = e.TryGetWebMessageAsString(); // lấy Thông điệp dưới dạng chuỗi.
                dynamic request = JsonConvert.DeserializeObject(message); // giải mã JSON 

                if (request.type == "analytics")
                {
                    try
                    {
                        _typeDate = request.dateRange.ToString();

                        var analyticsData = await _analyticsService.GetAnalyticsData(request.dateRange.ToString());
                        await _webView.ExecuteScriptAsync($"updateAnalyticsDashboard({JsonConvert.SerializeObject(analyticsData)})");

                        var pageTitleAnalytics = await _analyticsService.GetPageTitleAnalyticsAsync(request.dateRange.ToString());
                        await _webView.ExecuteScriptAsync($"updatePageTitleAnalyticsTable({JsonConvert.SerializeObject(pageTitleAnalytics)})");

                        var activeUsersPerCity = await _analyticsService.GetActiveUsersByCityAsync(request.dateRange.ToString());
                        await _webView.ExecuteScriptAsync($"updateActiveUsersPerCityChart({JsonConvert.SerializeObject(activeUsersPerCity)})");

                        if (_pagePerformanceData != null)
                        {
                            toolStripComboBox.Enabled = true;
                        }
                        else
                        {
                            toolStripComboBox.Enabled = false;
                        }

                    }
                    catch (Exception ex)
                    {
                        await _webView.ExecuteScriptAsync($"alert('Lỗi từ AnalyticsService: {ex.Message.Replace("'", "\\'")}')");
                        toolStripComboBox.Enabled = true;

                    }
                }
                if (request.type == "searchConsole")
                {
                    try
                    {
                        _startDate = request.startDate.ToString();
                        _endDate = request.endDate.ToString();
                        _typeDateConso = request.typeDate.ToString();
                        var dailydata = await _searchConsoleService.GetDailyPerformanceData(request.startDate.ToString(), request.endDate.ToString());
                        await _webView.ExecuteScriptAsync($"updateSearchConsoleDashboard({JsonConvert.SerializeObject(dailydata)})");


                        string url = showUrlsite.Text.Replace("SiteURL: https://", "").Trim();
                        url = url.Split(".")[0];
                        var data = await _searchConsoleService.GetSearchConsoleData(request.startDate.ToString(), request.endDate.ToString(), $"{url}-{request.typeDate.ToString()}");

                        await _webView.ExecuteScriptAsync($"updateCombinedTable({JsonConvert.SerializeObject(data)})");

                    }
                    catch (Exception ex)
                    {
                        await _webView.ExecuteScriptAsync($"alert('Lỗi từ SearchConsoleService (updateSearchConsoleDashboard && updateCombinedTable): {ex.Message.Replace("'", "\\'")}')");
                    }
                }
                else if (request.type == "pagePerformance")
                {
                    try
                    {
                        await _webView.ExecuteScriptAsync($"updatePerformanceTable(null,true)");
                        _pagePerformanceData = await _searchConsoleService.GetPagePerformanceData(request.startDate.ToString(), request.endDate.ToString());

                        await _webView.ExecuteScriptAsync($"updatePerformanceTable({JsonConvert.SerializeObject(_pagePerformanceData)})");

                        //await _webView.ExecuteScriptAsync("alert('Đã tải xong toàn bộ dữ liệu!')");
                        await _webView.ExecuteScriptAsync(@"
                            (function() {
                                let t = document.createElement('div');
                                t.textContent = 'Đã tải xong toàn bộ dữ liệu!';
                                Object.assign(t.style, {
                                    position: 'fixed', bottom: '40px', left: '50%', transform: 'translateX(-50%)',
                                    background: '#2ecc71', color: 'white', padding: '14px 32px', borderRadius: '10px',
                                    fontSize: '18px', fontWeight: 'bold', zIndex: '9999', opacity: '0',
                                    boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
                                    transition: 'opacity 0.5s ease'
                                });
                                document.body.appendChild(t);
                                setTimeout(() => t.style.opacity = '1', 100);
                                setTimeout(() => { t.style.opacity = '0'; setTimeout(() => t.remove(), 500); }, 3000);
                            })();
                        "); //thông báo tải xong
                        toolStripComboBox.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        await _webView.ExecuteScriptAsync($"alert('Lỗi từ SearchConsoleService (pagePerformance): {ex.Message.Replace("'", "\\'")}')");
                    }

                }
                else if (request.type == "getKeywordsForPage")
                {
                    try
                    {
                        var data = await _searchConsoleService.GetKeywordDataForPageAsync(
                            request.page.ToString(),
                            Convert.ToInt32(request.pageClicks),
                            Convert.ToInt32(request.pageImpressions),
                            float.Parse(request.pageCtr.ToString()),
                            float.Parse(request.pageAvgPosition.ToString()),
                            request.startDate.ToString(),
                            request.endDate.ToString()
                        );
                        await _webView.ExecuteScriptAsync($"updateDanhGiaTable({JsonConvert.SerializeObject(data)})");
                        toolStripComboBox.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        await _webView.ExecuteScriptAsync(
                            $"alert('Lỗi từ SearchConsoleService (updateDanhGiaTable): {ex.Message.Replace("'", "\\'")}')"
                        );
                    }
                }
                else if (request.type == "exportOldPosition")
                {
                    try
                    {
                        var data = await _searchConsoleService.GetSearchConsoleData(request.startDate.ToString(), request.endDate.ToString(), request.typeDate.ToString());

                        // Tạo danh sách các chuỗi cần ghi vào file
                        List<string> data2 = new List<string>();
                        foreach (var item in data)
                        {
                            string dtItem = item.Keyword + "==" + item.AvgPosition;
                            data2.Add(dtItem);
                        }
                        string url = showUrlsite.Text.Replace("SiteURL: https://", "").Trim();
                        url = url.Split(".")[0];

                        // Đường dẫn đến file txt lưu
                        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{url}-{request.typeDate}.txt");

                        // Ghi danh sách vào file
                        File.WriteAllLines(filePath, data2);
                        await _webView.ExecuteScriptAsync(@"
                            (function() {
                                let t = document.createElement('div');
                                t.textContent = 'Lưu vị trí thành công!';
                                Object.assign(t.style, {
                                    position: 'fixed', bottom: '40px', left: '50%', transform: 'translateX(-50%)',
                                    background: '#2ecc71', color: 'white', padding: '14px 32px', borderRadius: '10px',
                                    fontSize: '18px', fontWeight: 'bold', zIndex: '9999', opacity: '0',
                                    boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
                                    transition: 'opacity 0.5s ease'
                                });
                                document.body.appendChild(t);
                                setTimeout(() => t.style.opacity = '1', 10);
                                setTimeout(() => { t.style.opacity = '0'; setTimeout(() => t.remove(), 500); }, 2000);
                            })();
                        "); //thông báo lưu vị trí thành công
                        if (_pagePerformanceData != null)
                        {
                            toolStripComboBox.Enabled = true;
                        }
                        else
                        {
                            toolStripComboBox.Enabled = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        await _webView.ExecuteScriptAsync($"alert('Không có dữ liệu vị trí')");
                        toolStripComboBox.Enabled = true;
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                await _webView.ExecuteScriptAsync($"alert('Lỗi khi xử lý JSON: {jsonEx.Message.Replace("'", "\\'")}')");
            }
            catch (Exception ex)
            {
                await _webView.ExecuteScriptAsync($"alert('Lỗi tổng quát: {ex.Message.Replace("'", "\\'")}')");
            }
        }

        private List<(string UrlPage, string PropertyId)> GetWebsitesData()
        {
            var websitesData = new List<(string UrlPage, string PropertyId)>();
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SqlServer_string.txt");

            string connectionString = "";

            try
            {
                if (File.Exists(filePath))
                {
                    connectionString = File.ReadAllText(filePath).Trim();
                }
                else
                {
                    MessageBox.Show("Không tìm thấy file SqlServer_string.txt.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return websitesData;
                }
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT url_page, analytics_propertyId FROM websites"; // Không còn cột credentials_json
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            websitesData.Add((
                                UrlPage: reader["url_page"].ToString(),
                                PropertyId: reader["analytics_propertyId"].ToString()
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu từ database: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return websitesData;
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new WebsitesManagement().Show();
        }

        private void toolStripComboBox_Click(object sender, EventArgs e) //show danh sach website
        {
            var comboBox = sender as ToolStripComboBox;
            if (comboBox == null) return;

            comboBox.Items.Clear(); // Clear existing items

            var websitesData = GetWebsitesData();
            foreach (var website in websitesData)
            {
                comboBox.Items.Add(website.UrlPage); // Add url_page to the combo box
            }
        }

        private async void toolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var comboBox = sender as ToolStripComboBox;
            if (comboBox == null || comboBox.SelectedItem == null) return;

            this.ActiveControl = null;

            string selectedUrlPage = comboBox.SelectedItem.ToString();
            var websitesData = GetWebsitesData();

            var selectedWebsite = websitesData.Find(w => w.UrlPage == selectedUrlPage);
            if (selectedWebsite != default)
            {
                // Write data to files
                File.WriteAllText(propertyIdPath, selectedWebsite.PropertyId);
                File.WriteAllText(SearchConsole_SiteURL, selectedWebsite.UrlPage);

                // Reload services
                ReloadData();
                toolStripComboBox.Enabled = false;
                try
                {
                    var reloadanalytcis = await _analyticsService.GetAnalyticsData(_typeDate);
                    await _webView.ExecuteScriptAsync($"updateAnalyticsDashboard({JsonConvert.SerializeObject(reloadanalytcis)})");

                    var reloadpagetitleanalytics = await _analyticsService.GetPageTitleAnalyticsAsync(_typeDate);
                    await _webView.ExecuteScriptAsync($"updatePageTitleAnalyticsTable({JsonConvert.SerializeObject(reloadpagetitleanalytics)})");

                    var reloadactiveuserspercity = await _analyticsService.GetActiveUsersByCityAsync(_typeDate);
                    await _webView.ExecuteScriptAsync($"updateActiveUsersPerCityChart({JsonConvert.SerializeObject(reloadactiveuserspercity)})");
                    var dailydata = await _searchConsoleService.GetDailyPerformanceData(_startDate.ToString(), _endDate.ToString());
                    await _webView.ExecuteScriptAsync($"updateSearchConsoleDashboard({JsonConvert.SerializeObject(dailydata)})");

                    string url = showUrlsite.Text.Replace("SiteURL: https://", "").Trim();
                    url = url.Split(".")[0];
                    var reloadconsoledata = await _searchConsoleService.GetSearchConsoleData(_startDate.ToString(), _endDate.ToString(), $"{url}-{_typeDateConso.ToString()}");
                    await _webView.ExecuteScriptAsync($"updateCombinedTable({JsonConvert.SerializeObject(reloadconsoledata)})");

                    await _webView.ExecuteScriptAsync($"updateDanhGiaTable({JsonConvert.SerializeObject(null)})");

                   await _webView.ExecuteScriptAsync($"updatePerformanceTable(null,true)");
                    var reloadpageperformance = await _searchConsoleService.GetPagePerformanceData(_startDate.ToString(), _endDate.ToString());
                    await _webView.ExecuteScriptAsync($"updatePerformanceTable({JsonConvert.SerializeObject(reloadpageperformance)})");

                    await _webView.ExecuteScriptAsync(@"
                            (function() {
                                let t = document.createElement('div');
                                t.textContent = 'Đã tải xong toàn bộ dữ liệu!';
                                Object.assign(t.style, {
                                    position: 'fixed', bottom: '40px', left: '50%', transform: 'translateX(-50%)',
                                    background: '#2ecc71', color: 'white', padding: '14px 32px', borderRadius: '10px',
                                    fontSize: '18px', fontWeight: 'bold', zIndex: '9999', opacity: '0',
                                    boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
                                    transition: 'opacity 0.5s ease'
                                });
                                document.body.appendChild(t);
                                setTimeout(() => t.style.opacity = '1', 100);
                                setTimeout(() => { t.style.opacity = '0'; setTimeout(() => t.remove(), 500); }, 3000);
                            })();
                        "); //thông báo tải xong
                    toolStripComboBox.Enabled = true;

                }
                catch (Exception ex)
                {
                    await _webView.ExecuteScriptAsync($"alert('Lỗi : {ex.Message.Replace("'", "\\'")}')");
                }

            }
            else
            {
                MessageBox.Show("Không tìm thấy dữ liệu cho URL đã chọn.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripComboBox.Text = showUrlsite.Text.Replace("SiteURL: ","").Trim();
            toolStripComboBox.Enabled = false;
        }
    }
}