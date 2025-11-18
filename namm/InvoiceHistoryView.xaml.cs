﻿﻿﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho màn hình Lịch sử Hóa đơn.
    public partial class InvoiceHistoryView : UserControl
    {
        // Chuỗi kết nối CSDL.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ danh sách hóa đơn từ CSDL.
        private DataTable invoiceDataTable = new DataTable();

        public InvoiceHistoryView()
        {
            InitializeComponent();
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Lấy ngày có hóa đơn đầu tiên từ CSDL để làm ngày bắt đầu mặc định.
            DateTime? firstInvoiceDate = await GetFirstInvoiceDateAsync();

            // Thiết lập khoảng thời gian lọc mặc định.
            dpStartDate.SelectedDate = firstInvoiceDate?.Date ?? DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;

            BtnFilter_Click(sender, e); // Tải dữ liệu lần đầu với bộ lọc mặc định.
        }

        // Xử lý khi nhấn nút "Lọc".
        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra xem người dùng đã chọn ngày chưa.
            if (dpStartDate.SelectedDate == null || dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày bắt đầu và ngày kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lấy khoảng thời gian từ các DatePicker.
            DateTime startDate = dpStartDate.SelectedDate.Value.Date;
            DateTime endDate = dpEndDate.SelectedDate.Value.Date.AddDays(1).AddTicks(-1); // Lấy đến cuối ngày

            await LoadInvoicesAsync(startDate, endDate); // Tải dữ liệu hóa đơn.
        }

        // Hàm chính để tải danh sách hóa đơn từ CSDL dựa trên khoảng thời gian.
        private async Task LoadInvoicesAsync(DateTime startDate, DateTime endDate)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy thông tin hóa đơn đã thanh toán (Status = 1).
                const string query = @"
                    SELECT 
                        b.ID, 
                        ISNULL(c.CustomerCode, b.GuestCustomerCode) as CustomerCode,
                        b.DateCheckOut, 
                        ISNULL(c.Name, N'Khách vãng lai') AS CustomerName, 
                        tf.Name AS TableName, 
                        b.TotalAmount,
                        b.SubTotal
                    FROM Bill b
                    LEFT JOIN Customer c ON b.IdCustomer = c.ID
                    JOIN TableFood tf ON b.TableID = tf.ID
                    WHERE b.Status = 1 AND b.DateCheckOut BETWEEN @StartDate AND @EndDate
                    ORDER BY b.DateCheckOut DESC";

                // Tạo và cấu hình SqlDataAdapter.
                var adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddWithValue("@StartDate", startDate);
                adapter.SelectCommand.Parameters.AddWithValue("@EndDate", endDate);

                invoiceDataTable = new DataTable();
                invoiceDataTable.Columns.Add("STT", typeof(int)); // Thêm cột tạm thời để đánh số thứ tự.

                // Chạy tác vụ lấy dữ liệu trên một luồng nền để không làm treo giao diện.
                await Task.Run(() => adapter.Fill(invoiceDataTable));

                // Xử lý dữ liệu sau khi tải: bỏ chữ "Bàn " khỏi tên bàn.
                for (int i = 0; i < invoiceDataTable.Rows.Count; i++)
                {
                    string tableName = invoiceDataTable.Rows[i]["TableName"].ToString();
                    invoiceDataTable.Rows[i]["TableName"] = tableName.Replace("Bàn ", "");
                }

                dgInvoices.ItemsSource = invoiceDataTable.DefaultView;
                CalculateTotalRevenue(); // Tính tổng doanh thu.
                ClearSelection(); // Xóa khung xem trước.
            }
        }

        // Lấy ngày có hóa đơn sớm nhất trong CSDL.
        private async Task<DateTime?> GetFirstInvoiceDateAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT MIN(DateCheckOut) FROM Bill WHERE Status = 1";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return (DateTime)result;
                }
            }
            return null; // Trả về null nếu không có hóa đơn nào.
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
                    dpEndDate.SelectedDate = dpStartDate.SelectedDate;
                }
            }
        }

        // Sự kiện được gọi khi người dùng chọn một hóa đơn trong DataGrid.
        private async void DgInvoices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgInvoices.SelectedItem is DataRowView selectedInvoice)
            {
                int billId = (int)selectedInvoice["ID"];
                var detailsView = await LoadInvoiceDetailsAsync(billId); // Tải chi tiết của hóa đơn đó.

                // Hiển thị thông tin lên control xem trước.
                invoicePreview.DisplayInvoice(selectedInvoice, detailsView);
            }
            else
            {
                ClearSelection();
            }
        }

        // Tải chi tiết (các món hàng) của một hóa đơn cụ thể.
        private async Task<DataView> LoadInvoiceDetailsAsync(int billId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy các món hàng trong hóa đơn.
                const string query = @"
                    SELECT 
                        d.Name + N' (' + 
                            CASE bi.DrinkType 
                                WHEN N'Pha chế' THEN N'PC' 
                                WHEN N'Nguyên bản' THEN N'NB' 
                                ELSE bi.DrinkType 
                            END 
                        + N')' AS DrinkName, 
                        bi.Quantity, 
                        bi.Price, 
                        (bi.Quantity * bi.Price) AS TotalPrice
                    FROM BillInfo bi
                    JOIN Drink d ON bi.DrinkID = d.ID
                    WHERE bi.BillID = @BillID";

                var adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddWithValue("@BillID", billId);

                var detailsTable = new DataTable();
                await Task.Run(() => adapter.Fill(detailsTable));

                return detailsTable.DefaultView;
            }
        }

        // Tính tổng doanh thu từ bảng dữ liệu đã tải và hiển thị lên giao diện.
        private void CalculateTotalRevenue()
        {
            decimal total = 0;
            foreach (DataRow row in invoiceDataTable.Rows)
            {
                total += (decimal)row["TotalAmount"];
            }
            tbTotalRevenue.Text = $"{total:N0} VNĐ";
        }

        // Xóa nội dung của khung xem trước hóa đơn.
        private void ClearSelection()
        {
            invoicePreview.Clear();
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgInvoices_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView.Row["STT"] = e.Row.GetIndex() + 1;
            }
        }
    }
}