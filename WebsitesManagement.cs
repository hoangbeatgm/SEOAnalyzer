using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Windows.Forms;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.GoogleAnalyticsAdmin.v1alpha;
using Google.Apis.GoogleAnalyticsAdmin.v1alpha.Data;
using System.Net;
using System.Diagnostics;

namespace SEOAnalyzer
{
    public partial class WebsitesManagement : Form
    {
        string credentialsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json");
        private readonly WebView2 _webView;
        //private readonly string connectionString = "Server=DESKTOP-HA\\SQLEXPRESS;Database=SEOAnalyzer;Trusted_Connection=SSPI;Encrypt=false;TrustServerCertificate=true";
        private readonly string connectionString;
        public WebsitesManagement()
        {
            InitializeComponent();
            _webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(_webView);
            connectionString = GetConnectionStringFromFile();


            InitializeWebView();
        }
        private string GetConnectionStringFromFile()
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SqlServer_string.txt");

            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Không tìm thấy file chuỗi kết nối: {filePath}");
                }

                string connectionString = File.ReadAllText(filePath).Trim();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidDataException("Chuỗi kết nối trong file SqlServer_string.txt không hợp lệ.");
                }

                return connectionString;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đọc chuỗi kết nối: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit(); // Thoát ứng dụng nếu không thể đọc chuỗi kết nối
                return null; // Chỉ để tránh lỗi biên dịch, không bao giờ được thực thi
            }
        }
        private async void InitializeWebView()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync();
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

                string webPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebsitesManagement", "Websites.html");
                _webView.Source = new Uri($"file:///{webPath.Replace("\\", "/")}");

                // Đợi WebView hoàn tất tải trang trước khi load dữ liệu
                _webView.CoreWebView2.NavigationCompleted += (sender, e) =>
                {
                    if (e.IsSuccess)
                    {
                        LoadWebsitesFromDatabase();
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khởi tạo WebView: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadWebsitesFromDatabase()
        {
            try
            {
                var websites = new List<object>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT * FROM websites";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            websites.Add(new
                            {
                                id = reader["id"].ToString(),
                                analytics_propertyId = reader["analytics_propertyId"].ToString(),
                                url_page = reader["url_page"].ToString(),
                                created_at = Convert.ToDateTime(reader["created_at"]).ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                    }
                }

                string json = JsonConvert.SerializeObject(websites);
                string jsCode = $"loadWebsitesData({json});";
                _webView.CoreWebView2.ExecuteScriptAsync(jsCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dữ liệu từ database: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private string GetPropertyIdFromUrl(string urlPage)
        {
            const int maxRetries = 3;
            int retryCount = 0;
            TimeSpan delay = TimeSpan.FromSeconds(2);

            while (retryCount < maxRetries)
            {
                try
                {
                    // Kiểm tra URL hợp lệ
                    if (!Uri.TryCreate(urlPage, UriKind.Absolute, out Uri uri))
                    {
                        MessageBox.Show("URL không hợp lệ. Vui lòng nhập URL đầy đủ (bao gồm http:// hoặc https://)");
                        return null;
                    }

                    string domain = uri.Host;

                    // Tải credentials và tạo service
                    var credential = GoogleCredential.FromFile(credentialsPath)
                        .CreateScoped(new[] {
                    GoogleAnalyticsAdminService.Scope.AnalyticsReadonly,
                    GoogleAnalyticsAdminService.Scope.AnalyticsEdit
                        });

                    var service = new GoogleAnalyticsAdminService(new BaseClientService.Initializer()
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "SEO Analyzer",
                    });

                    // Lấy danh sách tài khoản GA4
                    var accounts = service.Accounts.List().Execute();
                    if (accounts?.Accounts == null || accounts.Accounts.Count == 0)
                    {
                        MessageBox.Show("Không tìm thấy tài khoản Google Analytics nào.");
                        return null;
                    }

                    // Tìm property phù hợp với domain
                    foreach (var account in accounts.Accounts)
                    {
                        var propertiesRequest = service.Properties.List();
                        propertiesRequest.Filter = $"parent:{account.Name}";
                        var properties = propertiesRequest.Execute();

                        foreach (var property in properties.Properties)
                        {
                            if (property?.DisplayName == null) continue;

                            // So sánh domain với DisplayName (có thể điều chỉnh logic so sánh tùy nhu cầu)
                            if (property.DisplayName.Contains(domain, StringComparison.OrdinalIgnoreCase) ||
                                property.DisplayName.Equals(domain, StringComparison.OrdinalIgnoreCase))
                            {
                                // Trả về Property ID (phần cuối của property.Name)
                                return property.Name.Split('/').Last();
                            }
                        }
                    }

                    MessageBox.Show(
                    $"Không tìm thấy website nào trong Google Analytics có tên hoặc tên miền trùng với: {domain}",
                    "Không Tìm Thấy Website",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                    return null;
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
                {
                    retryCount++;

                    if (ex.Error?.Message?.Contains("API has not been used") ?? false)
                    {
                        var result = MessageBox.Show(
                            "Google Analytics Admin API chưa được kích hoạt. Bạn có muốn mở trang kích hoạt ngay bây giờ không?",
                            "API Chưa Kích Hoạt",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (result == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = $"https://console.developers.google.com/apis/api/analyticsadmin.googleapis.com/overview?project=1037634009132",
                                UseShellExecute = true
                            });
                        }
                        return null;
                    }
                    else if (retryCount < maxRetries)
                    {
                        Thread.Sleep(delay);
                        continue;
                    }

                    MessageBox.Show($"Lỗi truy cập API: {ex.Message}\nVui lòng kiểm tra lại quyền truy cập.", "Lỗi API", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
                catch (Exception ex)
                {
                    if (retryCount < maxRetries)
                    {
                        retryCount++;
                        Thread.Sleep(delay);
                        continue;
                    }

                    MessageBox.Show($"Lỗi khi lấy Property ID: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
            }

            return null;
        }



        private void AddWebsiteToDatabase(string urlPage)
        {
            try
            {
                // Lấy analytics_propertyId từ URL trang
                string propertyId = GetPropertyIdFromUrl(urlPage);

                if (string.IsNullOrEmpty(propertyId))
                {
                    _webView.CoreWebView2.ExecuteScriptAsync("showToast('Không lấy được Analytics Property ID.');");
                    return;
                }

                // Lấy credentials từ file JSON

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO websites (analytics_propertyId, url_page, created_at) " +
                                   "VALUES (@propertyId, @urlPage, @createdAt)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@propertyId", propertyId);
                        cmd.Parameters.AddWithValue("@urlPage", urlPage);
                        cmd.Parameters.AddWithValue("@createdAt", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadWebsitesFromDatabase();
                _webView.CoreWebView2.ExecuteScriptAsync("showToast('Thêm website thành công!');");
            }
            catch (Exception ex)
            {
                _webView.CoreWebView2.ExecuteScriptAsync($"showToast('Lỗi khi thêm website: {ex.Message.Replace("'", "\\'")}');");
            }
        }


        private void EditWebsiteInDatabase(string id, string urlPage)
        {
            try
            {
                // Lấy analytics_propertyId từ URL trang
                string propertyId = GetPropertyIdFromUrl(urlPage);

                if (string.IsNullOrEmpty(propertyId))
                {
                    _webView.CoreWebView2.ExecuteScriptAsync("showToast('Không thể lấy Analytics Property ID.');");
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE websites SET analytics_propertyId = @propertyId, " +
                                    "url_page = @urlPage" +
                                    "WHERE id = @id";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@propertyId", propertyId);
                        cmd.Parameters.AddWithValue("@urlPage", urlPage);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadWebsitesFromDatabase();
                _webView.CoreWebView2.ExecuteScriptAsync("showToast('Cập nhật website thành công!');");
            }
            catch (Exception ex)
            {
                _webView.CoreWebView2.ExecuteScriptAsync($"showToast('Lỗi khi cập nhật website: {ex.Message.Replace("'", "\\'")}');");
            }
        }

        private void DeleteWebsiteFromDatabase(string id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "DELETE FROM websites WHERE id = @id";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            LoadWebsitesFromDatabase();
                            _webView.CoreWebView2.ExecuteScriptAsync("showToast('Xóa website thành công!');");
                        }
                        else
                        {
                            _webView.CoreWebView2.ExecuteScriptAsync("showToast('Không tìm thấy website để xóa!');");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string safeMessage = ex.Message.Replace("'", "\\'").Replace("\n", " ");
                _webView.CoreWebView2.ExecuteScriptAsync($"showToast('Lỗi khi xóa website: {safeMessage}');");
            }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                dynamic data = JsonConvert.DeserializeObject(message);

                switch (data.type.ToString())
                {
                    case "addWebsite":
                        AddWebsiteToDatabase(
                            data.data.url_page.ToString());
                        break;

                    case "editWebsite":
                        EditWebsiteInDatabase(
                            data.data.id.ToString(),

                            data.data.url_page.ToString());
                        break;

                    case "deleteWebsite":
                        // Sửa lại cách lấy ID từ message
                        string websiteId = data.id?.ToString() ?? data.data?.id?.ToString();
                        if (!string.IsNullOrEmpty(websiteId))
                        {
                            DeleteWebsiteFromDatabase(websiteId);
                        }
                        else
                        {
                            _webView.CoreWebView2.ExecuteScriptAsync("showToast('Không tìm thấy ID website để xóa!');");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                string safeMessage = ex.Message.Replace("'", "\\'").Replace("\n", " ");
                _webView.CoreWebView2.ExecuteScriptAsync($"showToast('Lỗi khi xử lý yêu cầu: {safeMessage}');");
            }
        }
    }
}