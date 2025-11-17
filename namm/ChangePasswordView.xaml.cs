﻿using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho màn hình Đổi mật khẩu (ChangePasswordView.xaml).
    public partial class ChangePasswordView : UserControl
    {
        // Sự kiện để thông báo cho cửa sổ chính (MainAppWindow) rằng cần phải đăng xuất.
        public event EventHandler? LogoutRequested;

        // Biến lưu thông tin tài khoản đang đăng nhập.
        private AccountDTO loggedInAccount;
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        // Hàm khởi tạo, nhận vào thông tin tài khoản đã đăng nhập.
        public ChangePasswordView(AccountDTO account)
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
            this.loggedInAccount = account; // Lưu lại thông tin tài khoản.
            txtUserName.Text = loggedInAccount.UserName; // Hiển thị tên đăng nhập lên giao diện.
        }

        // Xử lý sự kiện khi người dùng nhấn nút "Cập nhật".
        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            // Lấy giá trị từ các ô nhập mật khẩu.
            string oldPassword = pwbOldPassword.Password;
            string newPassword = pwbNewPassword.Password;
            string confirmPassword = pwbConfirmPassword.Password;

            // Kiểm tra xem người dùng đã nhập đủ thông tin chưa.
            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Kiểm tra xem mật khẩu mới và mật khẩu xác nhận có trùng khớp không.
            if (newPassword != confirmPassword)
            {
                MessageBox.Show("Mật khẩu mới và mật khẩu xác nhận không trùng khớp.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Kiểm tra xem mật khẩu cũ người dùng nhập có đúng với trong CSDL không.
            if (!CheckOldPassword(loggedInAccount.UserName, oldPassword))
            {
                MessageBox.Show("Mật khẩu cũ không chính xác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Nếu mọi thứ hợp lệ, tiến hành cập nhật mật khẩu mới vào CSDL.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Account SET Password = @NewPassword WHERE UserName = @UserName";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@NewPassword", newPassword);
                command.Parameters.AddWithValue("@UserName", loggedInAccount.UserName);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Đổi mật khẩu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                // Kích hoạt sự kiện LogoutRequested để cửa sổ chính xử lý việc đăng xuất.
                LogoutRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        // Hàm kiểm tra mật khẩu cũ bằng cách truy vấn CSDL.
        private bool CheckOldPassword(string userName, string oldPassword)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT COUNT(1) FROM Account WHERE UserName = @UserName AND Password = @Password";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@Password", oldPassword);
                connection.Open();
                // Thực thi câu lệnh và lấy kết quả (số dòng tìm thấy).
                int count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0; // Nếu có dòng được tìm thấy (count > 0), mật khẩu đúng.
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        // Hàm để xóa trắng các ô nhập liệu.
        private void ResetFields()
        {
            pwbOldPassword.Clear();
            pwbNewPassword.Clear();
            pwbConfirmPassword.Clear();
        }
    }
}