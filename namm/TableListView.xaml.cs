﻿// File này chứa logic cho màn hình hiển thị danh sách bàn ăn (TableListView.xaml).
// Chức năng chính là tải danh sách các bàn từ CSDL và hiển thị chúng dưới dạng các nút bấm (Button)
// với màu sắc khác nhau tùy theo trạng thái (Trống/Có người).
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace namm
{
    // Lớp xử lý logic cho UserControl TableListView.
    public partial class TableListView : UserControl
    {
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        // Hàm khởi tạo của UserControl.
        public TableListView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTableList(); // Gọi hàm để tải và hiển thị danh sách bàn.
        }

        // Hàm tải danh sách bàn từ CSDL và tạo các nút bấm tương ứng.
        void LoadTableList()
        {
            wpTables.Children.Clear(); // Xóa tất cả các bàn cũ (nếu có) trước khi tải lại để tránh trùng lặp.
            // 'using' đảm bảo kết nối được đóng lại ngay cả khi có lỗi.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy tên và trạng thái của tất cả các bàn.
                const string query = "SELECT Name, Status FROM TableFood";
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open(); // Mở kết nối đến CSDL.
                SqlDataReader reader = command.ExecuteReader();
                // Đọc từng dòng kết quả trả về từ câu lệnh SQL.
                while (reader.Read())
                {
                    string tableName = reader["Name"].ToString(); // Lấy tên bàn.
                    string status = reader["Status"].ToString(); // Lấy trạng thái bàn ("Trống" hoặc "Có người").

                    // Tạo một nút bấm mới cho mỗi bàn.
                    Button tableButton = new Button
                    {
                        // Thiết lập nội dung cho nút, bao gồm tên bàn và trạng thái. Environment.NewLine để xuống dòng.
                        Content = tableName + Environment.NewLine + (status == "Trống" ? "(Trống)" : "(Có người)"),
                        Width = 120,
                        Height = 120,
                        Margin = new Thickness(10),
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Tag = tableName // Lưu tên bàn vào thuộc tính Tag để có thể lấy lại khi người dùng nhấn vào nút.
                    };

                    // Đặt màu nền cho nút dựa trên trạng thái của bàn.
                    tableButton.Background = status == "Trống" ? Brushes.LightGreen : Brushes.LightCoral;

                    // Thêm nút vừa tạo vào WrapPanel có tên là 'wpTables' trên giao diện.
                    wpTables.Children.Add(tableButton);
                }
            }
        }
    }
}