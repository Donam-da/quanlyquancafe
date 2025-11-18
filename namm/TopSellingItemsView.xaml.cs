﻿// File này chứa logic cho màn hình Thống kê Top hàng bán chạy (TopSellingItemsView.xaml).
// Chức năng chính bao gồm:
// 1. Lọc và hiển thị danh sách các món hàng bán chạy nhất trong một khoảng thời gian do người dùng chọn.
// 2. Tổng hợp số lượng bán và tổng doanh thu cho mỗi món.
// 3. Cho phép lọc kết quả trực tiếp trên bảng theo tên, mã, và kiểu đồ uống.
using System;
using System.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho UserControl TopSellingItemsView.
    public partial class TopSellingItemsView : UserControl
    {
        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ kết quả thống kê từ CSDL.
        private DataTable topItemsTable = new DataTable();

        // Hàm khởi tạo của UserControl.
        public TopSellingItemsView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Thiết lập khoảng thời gian lọc mặc định là ngày hôm nay.
            dpStartDate.SelectedDate = DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;

            // Thêm các mục vào ComboBox để lọc theo kiểu đồ uống.
            cbFilterDrinkType.Items.Add("Tất cả"); // Mục để hiển thị tất cả các kiểu.
            cbFilterDrinkType.Items.Add("Pha chế");
            cbFilterDrinkType.Items.Add("Nguyên bản");
            cbFilterDrinkType.SelectedIndex = 0; // Mặc định chọn "Tất cả".

            BtnFilter_Click(sender, e); // Tải dữ liệu lần đầu với bộ lọc mặc định.
        }

        // Xử lý sự kiện khi nhấn nút "Lọc".
        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem người dùng đã chọn ngày bắt đầu và kết thúc chưa.
            if (dpStartDate.SelectedDate == null || dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày bắt đầu và ngày kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lấy khoảng thời gian từ các DatePicker.
            DateTime startDate = dpStartDate.SelectedDate.Value.Date; // Lấy phần ngày, bỏ qua phần giờ.
            DateTime endDate = dpEndDate.SelectedDate.Value.Date.AddDays(1).AddTicks(-1); // Lấy đến cuối ngày được chọn (23:59:59.999).

            await LoadTopSellingItemsAsync(startDate, endDate); // Gọi hàm để tải dữ liệu thống kê.
        }

        // Hàm chính để tải dữ liệu thống kê từ CSDL.
        private async Task LoadTopSellingItemsAsync(DateTime startDate, DateTime endDate)
        {
            // 'using' đảm bảo kết nối được đóng lại ngay cả khi có lỗi.
            using (var connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để tổng hợp dữ liệu bán hàng.
                const string query = @"
                    SELECT
                        d.Name AS DrinkName, -- Tên đồ uống
                        d.DrinkCode, -- Mã đồ uống
                        bi.DrinkType, -- Kiểu (Pha chế/Nguyên bản)
                        SUM(bi.Quantity) AS TotalQuantitySold, -- Tổng số lượng bán được
                        SUM(bi.Quantity * bi.Price) AS TotalRevenue -- Tổng doanh thu (chưa trừ vốn)
                    FROM BillInfo bi
                    JOIN Bill b ON bi.BillID = b.ID -- Nối với bảng Bill để lấy thông tin hóa đơn
                    JOIN Drink d ON bi.DrinkID = d.ID -- Nối với bảng Drink để lấy thông tin đồ uống
                    WHERE b.Status = 1 AND b.DateCheckOut BETWEEN @StartDate AND @EndDate -- Chỉ lấy các hóa đơn đã thanh toán trong khoảng thời gian đã chọn
                    GROUP BY d.Name, d.DrinkCode, bi.DrinkType -- Nhóm các món giống nhau lại để tính tổng
                    ORDER BY TotalQuantitySold DESC; -- Sắp xếp theo số lượng bán giảm dần
                ";

                var adapter = new SqlDataAdapter(query, connection);
                // Thêm các tham số ngày vào câu lệnh SQL để tránh lỗi SQL Injection.
                adapter.SelectCommand.Parameters.AddWithValue("@StartDate", startDate);
                adapter.SelectCommand.Parameters.AddWithValue("@EndDate", endDate);

                topItemsTable = new DataTable();
                topItemsTable.Columns.Add("STT", typeof(int)); // Thêm cột "STT" để đánh số thứ tự.

                // Chạy tác vụ lấy dữ liệu trên một luồng nền để không làm treo giao diện.
                await Task.Run(() => adapter.Fill(topItemsTable));

                // Duyệt qua bảng dữ liệu để điền số thứ tự.
                for (int i = 0; i < topItemsTable.Rows.Count; i++)
                {
                    topItemsTable.Rows[i]["STT"] = i + 1;
                }

                dgTopSelling.ItemsSource = topItemsTable.DefaultView; // Gán dữ liệu cho DataGrid.
                ApplyColumnFilters(); // Áp dụng lại các bộ lọc cột hiện tại sau khi tải dữ liệu mới.
            }
        }

        // Sự kiện được gọi khi người dùng thay đổi ngày bắt đầu.
        private void DpStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Đảm bảo người dùng không thể chọn ngày kết thúc trước ngày bắt đầu.
            if (dpStartDate.SelectedDate.HasValue)
            {
                dpEndDate.DisplayDateStart = dpStartDate.SelectedDate;
                if (dpEndDate.SelectedDate < dpStartDate.SelectedDate)
                {
                    dpEndDate.SelectedDate = dpStartDate.SelectedDate; // Tự động cập nhật ngày kết thúc nếu nó không hợp lệ.
                }
            }
        }

        // Sự kiện được gọi khi nội dung trong các ô lọc văn bản thay đổi.
        private void Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyColumnFilters(); // Áp dụng bộ lọc.
        }

        // Sự kiện được gọi khi lựa chọn trong ComboBox lọc thay đổi.
        private void Filter_TextChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyColumnFilters(); // Áp dụng bộ lọc.
        }

        // Hàm áp dụng bộ lọc cho dữ liệu hiển thị trên DataGrid (lọc phía client).
        private void ApplyColumnFilters()
        {
            if (topItemsTable.DefaultView == null) return;

            var filters = new List<string>(); // Tạo một danh sách để chứa các điều kiện lọc.

            // Nếu có văn bản trong ô lọc tên đồ uống, thêm điều kiện lọc vào danh sách.
            if (!string.IsNullOrWhiteSpace(txtFilterDrinkName.Text))
            {
                filters.Add($"DrinkName LIKE '%{txtFilterDrinkName.Text.Replace("'", "''")}%'"); // Replace "'" để tránh lỗi cú pháp.
            }
            // Tương tự cho mã đồ uống.
            if (!string.IsNullOrWhiteSpace(txtFilterDrinkCode.Text))
            {
                filters.Add($"DrinkCode LIKE '%{txtFilterDrinkCode.Text.Replace("'", "''")}%'");
            }
            // Nếu người dùng chọn một kiểu cụ thể (không phải "Tất cả").
            if (cbFilterDrinkType.SelectedIndex > 0)
            {
                filters.Add($"DrinkType = '{cbFilterDrinkType.SelectedItem}'"); // Thêm điều kiện lọc theo kiểu.
            }
            // Kết hợp tất cả các điều kiện lọc bằng "AND" và gán cho RowFilter của DataView.
            // RowFilter sẽ tự động ẩn các dòng không thỏa mãn điều kiện mà không cần truy vấn lại CSDL.
            topItemsTable.DefaultView.RowFilter = string.Join(" AND ", filters);
        }
    }
}