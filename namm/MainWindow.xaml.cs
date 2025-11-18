﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Data.SqlClient;
using System.IO; // Added for File.Exists
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using namm.Properties;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace namm
{
    // Lớp MainWindow đại diện cho cửa sổ đăng nhập.
    public partial class MainWindow : Window
    {
        // Chuỗi kết nối đến cơ sở dữ liệu, được lấy từ file App.config.
        string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        // Hàm khởi tạo (constructor) của cửa sổ.
        public MainWindow()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
            LoadRememberedUser(); // Tải thông tin người dùng đã được lưu từ lần đăng nhập trước.
            this.Loaded += MainWindow_Loaded; // Gán một hàm để xử lý sự kiện khi cửa sổ đã được tải xong.
        }

        // Sự kiện được gọi khi cửa sổ đã tải xong hoàn toàn.
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCustomInterface(); // Tải giao diện tùy chỉnh (màu sắc, logo) một cách bất đồng bộ.
            txtUsername.Focus(); // Tự động đặt con trỏ vào ô nhập tên đăng nhập để người dùng có thể gõ ngay.
        }

        // Phương thức bất đồng bộ để tải giao diện tùy chỉnh từ CSDL.
        private async Task LoadCustomInterface()
        {
            try
            {
                // Tải màu nền cho icon từ file cài đặt của ứng dụng.
                string bgColor = Properties.Settings.Default.LoginIconBgColor;
                iconBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(bgColor);

                // Sử dụng 'using' để đảm bảo kết nối CSDL được đóng đúng cách.
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(); // Mở kết nối đến CSDL.
                    // Câu lệnh SQL để lấy dữ liệu ảnh (dạng byte) từ bảng InterfaceImages, nơi ảnh được đánh dấu là active.
                    var command = new SqlCommand("SELECT ImageData FROM InterfaceImages WHERE IsActiveForLogin = 1", connection);
                    command.CommandTimeout = 120; // Tăng thời gian chờ cho câu lệnh SQL lên 120 giây để tránh lỗi timeout.
                    var imageData = await command.ExecuteScalarAsync() as byte[]; // Thực thi câu lệnh và lấy kết quả (dữ liệu ảnh).

                    // Nếu có dữ liệu ảnh, tiến hành tải ảnh.
                    if (imageData != null && imageData.Length > 0)
                    {
                        // Chuyển đổi mảng byte thành ảnh trên một luồng nền để không làm treo giao diện.
                        imgLoginIcon.Source = await Task.Run(() => LoadImageFromBytes(imageData));
                    }
                    else
                    {
                        // Nếu không có ảnh trong CSDL, sử dụng ảnh mặc định từ tài nguyên của ứng dụng.
                        imgLoginIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                    }
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi (ví dụ: file ảnh bị xóa), sử dụng ảnh mặc định
                imgLoginIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                // Ghi lại thông tin lỗi vào cửa sổ Output để debug.
                Debug.WriteLine($"Lỗi khi tải giao diện tùy chỉnh: {ex.Message}");
            }
        }

        // Hàm trợ giúp để chuyển đổi một mảng byte (dữ liệu ảnh) thành đối tượng BitmapImage mà WPF có thể hiển thị.
        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            // Nếu không có dữ liệu, trả về ảnh mặc định.
            if (imageData == null || imageData.Length == 0) return new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));

            var image = new BitmapImage();
            // Sử dụng MemoryStream để đọc dữ liệu ảnh từ mảng byte trong bộ nhớ.
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0; // Đặt con trỏ về đầu stream.
                image.BeginInit(); // Bắt đầu quá trình khởi tạo ảnh.
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat; // Giữ nguyên định dạng pixel của ảnh gốc.
                image.CacheOption = BitmapCacheOption.OnLoad; // Tải toàn bộ ảnh vào bộ nhớ ngay lập tức.
                image.StreamSource = mem; // Nguồn dữ liệu của ảnh là MemoryStream.
                image.EndInit(); // Kết thúc quá trình khởi tạo.
            }
            image.Freeze(); // "Đóng băng" ảnh để tối ưu hóa hiệu suất, ngăn không cho nó thay đổi.
            return image; // Trả về đối tượng ảnh đã tạo.
        }

        // Tải thông tin đăng nhập đã lưu (nếu có).
        private void LoadRememberedUser()
        {
            // Kiểm tra xem người dùng có chọn "Lưu thông tin" ở lần đăng nhập trước không.
            if (Settings.Default.RememberMe)
            {
                // Nếu có, điền lại tên đăng nhập, mật khẩu và check vào ô "Lưu thông tin".
                txtUsername.Text = Settings.Default.Username;
                pwbPassword.Password = Settings.Default.Password;
                chkRememberMe.IsChecked = true;
            }
        }

        // Xử lý sự kiện khi người dùng nhấn nút "Đăng nhập".
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // Lấy tên đăng nhập và mật khẩu từ các ô nhập liệu.
            string username = txtUsername.Text;
            string password = pwbPassword.Password;

            // Kiểm tra xem người dùng đã nhập đủ thông tin chưa.
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập tên đăng nhập và mật khẩu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Gọi hàm để kiểm tra thông tin đăng nhập với CSDL.
            AccountDTO? loginAccount = CheckLogin(username, password);
            // Nếu hàm trả về một đối tượng tài khoản (đăng nhập thành công).
            if (loginAccount != null)
            {
                // Nếu ô "Lưu thông tin" được check.
                if (chkRememberMe.IsChecked == true)
                {
                    // Lưu tên đăng nhập, mật khẩu và trạng thái "Lưu thông tin" vào file cài đặt.
                    Settings.Default.Username = username;
                    Settings.Default.Password = password; // Cảnh báo: Lưu mật khẩu dạng văn bản thô (plain text) là không an toàn.
                    Settings.Default.RememberMe = true;
                }
                else
                {
                    // Nếu không check, xóa thông tin đã lưu.
                    Settings.Default.Username = "";
                    Settings.Default.Password = "";
                    Settings.Default.RememberMe = false;
                }
                Settings.Default.Save(); // Lưu các thay đổi vào file cài đặt.

                // Tạo một cửa sổ chính của ứng dụng và truyền thông tin tài khoản đã đăng nhập vào.
                MainAppWindow mainApp = new MainAppWindow(loginAccount);
                mainApp.Show(); // Hiển thị cửa sổ chính.
                this.Close(); // Đóng cửa sổ đăng nhập
            }
            else
            {
                // Nếu đăng nhập thất bại, hiển thị thông báo lỗi.
                MessageBox.Show("Tên đăng nhập hoặc mật khẩu không chính xác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Hàm kiểm tra thông tin đăng nhập bằng cách truy vấn CSDL.
        private AccountDTO? CheckLogin(string username, string password)
        {
            AccountDTO? account = null; // Biến để lưu thông tin tài khoản nếu tìm thấy.
            // Sử dụng 'using' để đảm bảo kết nối được đóng đúng cách.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để tìm tài khoản có UserName và Password khớp.
                string query = "SELECT * FROM Account WHERE UserName=@UserName AND Password=@Password";
                SqlCommand command = new SqlCommand(query, connection);
                // Sử dụng tham số hóa để tránh lỗi SQL Injection.
                command.Parameters.AddWithValue("@UserName", username);
                command.Parameters.AddWithValue("@Password", password); // Nhắc lại: Mật khẩu nên được mã hóa (hash) trước khi lưu và so sánh.

                connection.Open(); // Mở kết nối.
                SqlDataReader reader = command.ExecuteReader(); // Thực thi câu lệnh và lấy kết quả.
                // Nếu có dòng dữ liệu được trả về (tìm thấy tài khoản).
                if (reader.Read())
                {
                    // Tạo một đối tượng AccountDTO và điền thông tin từ CSDL vào.
                    account = new AccountDTO
                    {
                        UserName = reader["UserName"].ToString() ?? "",
                        DisplayName = reader["DisplayName"].ToString() ?? "",
                        Type = (int)reader["Type"],
                    };
                }
            }
            return account; // Trả về đối tượng tài khoản (nếu tìm thấy) hoặc null (nếu không tìm thấy).
        }

        // Xử lý sự kiện khi người dùng nhấn nút "Thoát".
        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown(); // Đóng toàn bộ ứng dụng.
        }
    }
}
