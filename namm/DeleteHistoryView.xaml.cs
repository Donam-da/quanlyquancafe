using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace namm
{
    // Lớp để lưu trữ thông tin ngày và số hóa đơn tương ứng (hiện không còn được sử dụng).
    public class DateInvoiceCount
    {
        public DateTime Date { get; set; }
        public int InvoiceCount { get; set; }
    }
    // Lớp xử lý logic cho màn hình Xóa Lịch sử Hóa đơn.
    public partial class DeleteHistoryView : UserControl
    {
        // Chuỗi kết nối CSDL.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Collection để lưu chi tiết các ngày đã chọn (hiện không còn được sử dụng).
        private readonly ObservableCollection<DateInvoiceCount> _selectedDatesDetails = new ObservableCollection<DateInvoiceCount>();

        public DeleteHistoryView()
        {
            InitializeComponent();
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Mặc định chọn ngày hôm nay để tránh người dùng vô tình xóa.
            dpDate.SelectedDate = DateTime.Today;
            tbResult.Text = "";
            await LoadCustomersAsync(); // Tải danh sách khách hàng cho ComboBox.
            // Gọi lần đầu để tải dữ liệu cho chế độ mặc định (Trước ngày).
            DeleteMode_Changed(null, null);
        }
        
        // Xử lý khi nhấn vào checkbox ở header của DataGrid để chọn/bỏ chọn tất cả.
        private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked ?? false;
            if (dgInvoicesToDelete.ItemsSource is DataView dataView)
            {
                // Duyệt qua tất cả các dòng và cập nhật cột 'IsSelected'.
                foreach (DataRowView rowView in dataView)
                    rowView["IsSelected"] = isChecked;
            }
        }

        // Sự kiện quan trọng: Xử lý khi nhấn nút "XÓA DỮ LIỆU".
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra các điều kiện đầu vào dựa trên chế độ xóa được chọn.
            if (rbOnDate.IsChecked != true && rbByCustomer.IsChecked != true && !dpDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Vui lòng chọn một ngày.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (rbOnDate.IsChecked == true && calendarMultiSelect.SelectedDates.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một ngày trên lịch.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (rbByCustomer.IsChecked == true && cbCustomers.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Lấy danh sách ID của các hóa đơn được check trong DataGrid.
            var selectedInvoiceIds = (dgInvoicesToDelete.ItemsSource as DataView)?
                .Cast<DataRowView>()
                .Where(row => (bool)row["IsSelected"])
                .Select(row => (int)row["ID"])
                .ToList() ?? new List<int>();

            // Nếu không có hóa đơn nào được chọn, hiển thị cảnh báo.
            if (!selectedInvoiceIds.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một hóa đơn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Tạo thông báo xác nhận dựa trên chế độ xóa.
            DateTime selectedDate = dpDate.SelectedDate.Value.Date;
            DateTime startDate;
            DateTime? endDate = null;
            string confirmationMessage;

            if (rbBeforeDate.IsChecked == true)
            {
                startDate = selectedDate;
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trước ngày {startDate:dd/MM/yyyy} không?";
            }
            else if (rbOnDate.IsChecked == true)
            {
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trong các ngày đã chọn không?";
            }
            else if (rbInWeek.IsChecked == true)
            {
                DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
                startDate = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)firstDayOfWeek);
                if (selectedDate.DayOfWeek < firstDayOfWeek) startDate = startDate.AddDays(-7);
                endDate = startDate.AddDays(7);
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trong tuần từ {startDate:dd/MM/yyyy} đến {endDate.Value.AddDays(-1):dd/MM/yyyy} không?";
            }
            else if (rbInMonth.IsChecked == true)
            {
                startDate = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                endDate = startDate.AddMonths(1);
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trong tháng {startDate:MM/yyyy} không?";
            }
            else // Chế độ theo khách hàng
            {
                var selectedCustomer = (DataRowView)cbCustomers.SelectedItem;
                string customerName = selectedCustomer["Name"].ToString();
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn của khách hàng '{customerName}' không?";
            }

            // Hiển thị hộp thoại xác nhận cuối cùng với cảnh báo quan trọng.
            var result = MessageBox.Show(
                $"{confirmationMessage}\n\nHÀNH ĐỘNG NÀY KHÔNG THỂ HOÀN TÁC!",
                "XÁC NHẬN XÓA VĨNH VIỄN",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No); // Mặc định là No

            if (result == MessageBoxResult.Yes)
            {
                tbResult.Text = "Đang xử lý, vui lòng chờ...";
                btnDelete.IsEnabled = false;

                try
                {
                    int billsDeleted = 0;
                    int billInfosDeleted = 0;
                    
                    // Tạo danh sách tham số SQL (@id0, @id1,...) để dùng trong mệnh đề IN, tránh SQL Injection.
                    var idParameters = selectedInvoiceIds.Select((id, index) => new SqlParameter($"@id{index}", id)).ToList();
                    var idParamNames = string.Join(", ", idParameters.Select(p => p.ParameterName));

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        // Sử dụng Transaction để đảm bảo cả hai lệnh xóa đều thành công hoặc không lệnh nào thành công.
                        using (var transaction = connection.BeginTransaction())
                        {
                            // 1. Xóa các chi tiết hóa đơn (BillInfo) trước để không vi phạm khóa ngoại.
                            string deleteBillInfoQuery = $"DELETE FROM BillInfo WHERE BillID IN ({idParamNames})";
                            var cmdBillInfo = new SqlCommand(deleteBillInfoQuery, connection, transaction);
                            cmdBillInfo.Parameters.AddRange(idParameters.ToArray());
                            billInfosDeleted = await cmdBillInfo.ExecuteNonQueryAsync();

                            // 2. Xóa các hóa đơn (Bill) sau.
                            string deleteBillQuery = $"DELETE FROM Bill WHERE ID IN ({idParamNames})";
                            var cmdBill = new SqlCommand(deleteBillQuery, connection, transaction);
                            // Phải tạo lại tham số vì một đối tượng SqlParameter không thể thuộc về hai SqlCommand khác nhau.
                            var idParametersForBill = selectedInvoiceIds.Select((id, index) => new SqlParameter($"@id{index}", id)).ToList();
                            cmdBill.Parameters.AddRange(idParametersForBill.ToArray());
                            billsDeleted = await cmdBill.ExecuteNonQueryAsync();

                            // Nếu cả hai lệnh xóa đều thành công, xác nhận thay đổi.
                            transaction.Commit();
                        }
                    }

                    tbResult.Text = $"Hoàn tất! Đã xóa thành công {billsDeleted} hóa đơn và {billInfosDeleted} chi tiết món.";
                }
                catch (Exception ex)
                {
                    tbResult.Text = "Đã xảy ra lỗi trong quá trình xóa.";
                    MessageBox.Show($"Lỗi khi xóa dữ liệu: {ex.Message}", "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnDelete.IsEnabled = true;
                    // Tải lại danh sách hóa đơn để cập nhật giao diện.
                    if (rbOnDate.IsChecked == true) 
                        await UpdateSelectedDatesDetailsAsync();
                    else if (rbByCustomer.IsChecked == true) 
                        await UpdateCustomerInvoiceInfo();
                    else await UpdateDateRangeInfo();
                }
            }
        }

        // Xử lý khi người dùng thay đổi chế độ xóa.
        private async void DeleteMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;

            // Ẩn/hiện các control tương ứng với chế độ được chọn.
            dpDate.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible;
            calendarMultiSelect.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            cbCustomers.Visibility = (rbByCustomer.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            
            // Cập nhật nhãn mô tả cho chế độ.
            if (rbBeforeDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn TRƯỚC ngày:";
            }
            else if (rbOnDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong CÁC NGÀY đã chọn:";
                await UpdateSelectedDatesDetailsAsync();
            }
            else if (rbInWeek.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong TUẦN chứa ngày:";
            }
            else // rbInMonth
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong THÁNG chứa ngày:";
            }

            if (rbByCustomer.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn của khách hàng:";
                dpDate.Visibility = Visibility.Collapsed;
                calendarMultiSelect.Visibility = Visibility.Collapsed;
                await UpdateCustomerInvoiceInfo();
                return; // Dừng lại, không cần gọi UpdateDateRangeInfo.
            }
            // Tải lại thông tin và danh sách hóa đơn cho các chế độ liên quan đến ngày.
            await UpdateDateRangeInfo();
        }

        // Xử lý khi người dùng thay đổi ngày trên DatePicker.
        private async void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateDateRangeInfo();
        }

        // Tải và hiển thị thông tin hóa đơn cho các chế độ theo ngày/tuần/tháng.
        private async Task UpdateDateRangeInfo()
        {
            if (!this.IsLoaded || rbOnDate.IsChecked == true || rbByCustomer.IsChecked == true)
            {
                tbDateRangeInfo.Visibility = Visibility.Collapsed;
                return;
            }

            // Dọn dẹp trước khi tải mới
            dgInvoicesToDelete.ItemsSource = null;
            invoicePreview.Clear(); // Xóa nội dung xem trước.

            if (!dpDate.SelectedDate.HasValue)
            {
                tbDateRangeInfo.Visibility = Visibility.Collapsed;
                return;
            }

            DateTime selectedDate = dpDate.SelectedDate.Value.Date;
            tbDateRangeInfo.Visibility = Visibility.Visible;
            int invoiceCount;

            // Tải danh sách hóa đơn dựa trên chế độ đang chọn.
            if (rbBeforeDate.IsChecked == true)
            {
                var invoices = await GetInvoicesBeforeDateAsync(selectedDate);
                invoiceCount = invoices.Rows.Count;
                dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
                tbDateRangeInfo.Text = $"(Sẽ xóa {invoiceCount} hóa đơn)";
            }
            else if (rbInWeek.IsChecked == true)
            {
                DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
                DateTime startDate = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)firstDayOfWeek);
                if (selectedDate.DayOfWeek < firstDayOfWeek) startDate = startDate.AddDays(-7);
                DateTime endDate = startDate.AddDays(7);
                var invoices = await GetInvoicesForDateRangeAsync(startDate, endDate);
                invoiceCount = invoices.Rows.Count;
                dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
                tbDateRangeInfo.Text = $"(Từ {startDate:dd/MM/yyyy} đến {endDate.AddDays(-1):dd/MM/yyyy} - có {invoiceCount} hóa đơn)";
            }
            else if (rbInMonth.IsChecked == true)
            {
                DateTime startDate = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                DateTime endDate = startDate.AddMonths(1);
                var invoices = await GetInvoicesForDateRangeAsync(startDate, endDate);
                invoiceCount = invoices.Rows.Count;
                dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
                tbDateRangeInfo.Text = $"(Trong tháng {startDate:MM/yyyy} - có {invoiceCount} hóa đơn)";
            }
            else 
            { 
                tbDateRangeInfo.Visibility = Visibility.Collapsed; 
            }
        }

        // Xử lý khi người dùng chọn/bỏ chọn các ngày trên lịch.
        private async void CalendarMultiSelect_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateSelectedDatesDetailsAsync();
        }

        // Xử lý khi người dùng chọn một khách hàng khác từ ComboBox.
        private async void CbCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateCustomerInvoiceInfo();
        }

        // Tải và hiển thị thông tin hóa đơn cho khách hàng được chọn.
        private async Task UpdateCustomerInvoiceInfo()
        {
            if (!this.IsLoaded || cbCustomers.SelectedItem == null)
            {
                tbDateRangeInfo.Visibility = Visibility.Collapsed;
                dgInvoicesToDelete.ItemsSource = null;
                invoicePreview.Clear();
                return;
            }

            dgInvoicesToDelete.ItemsSource = null;
            invoicePreview.Clear();

            var selectedCustomer = (DataRowView)cbCustomers.SelectedItem;
            int customerId = (int)selectedCustomer["ID"];
            string customerName = selectedCustomer["Name"].ToString();

            var invoices = await GetInvoicesForCustomerAsync(customerId);
            int invoiceCount = invoices.Rows.Count;

            dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
            tbDateRangeInfo.Visibility = Visibility.Visible;
            tbDateRangeInfo.Text = $"(Khách hàng '{customerName}' có {invoiceCount} hóa đơn)";
        }

        // Tải và hiển thị thông tin hóa đơn cho nhiều ngày được chọn trên lịch.
        private async Task UpdateSelectedDatesDetailsAsync()
        {
            if (!this.IsLoaded) return;

            dgInvoicesToDelete.ItemsSource = null;
            invoicePreview.Clear();

            var selectedDates = calendarMultiSelect.SelectedDates.OrderBy(d => d.Date).ToList();

            // Lấy danh sách hóa đơn cho các ngày đã chọn.
            var invoices = await GetInvoicesForMultipleDatesAsync(selectedDates);
            dgInvoicesToDelete.ItemsSource = invoices.DefaultView;

            // Tính và hiển thị thông tin tóm tắt.
            int totalInvoices = invoices.Rows.Count;
            tbDateRangeInfo.Visibility = Visibility.Visible;
            tbDateRangeInfo.Text = $"(Sẽ xóa {totalInvoices} hóa đơn từ {selectedDates.Count} ngày đã chọn)";
        }

        // Các phương thức dưới đây là các hàm truy cập dữ liệu (Data Access Layer) để lấy thông tin từ CSDL.
        #region Data Access Methods
        private async Task<int> GetInvoiceCountForDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            string query = "SELECT COUNT(ID) FROM Bill WHERE Status = 1 AND DateCheckOut >= @StartDate AND DateCheckOut < @EndDate";
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand(query, connection);
                command.Parameters.Add(new SqlParameter("@StartDate", startDate));
                command.Parameters.Add(new SqlParameter("@EndDate", endDate));
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            }
        }

        private async Task<int> GetInvoiceCountForSingleDateAsync(DateTime date)
        {
            return await GetInvoiceCountForDateRangeAsync(date.Date, date.Date.AddDays(1));
        }

        private async Task<int> GetInvoiceCountBeforeDateAsync(DateTime date)
        {
            string query = "SELECT COUNT(ID) FROM Bill WHERE Status = 1 AND DateCheckOut < @EndDate";
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand(query, connection);
                command.Parameters.Add(new SqlParameter("@EndDate", date));
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            }
        }

        private async Task<DataTable> GetInvoicesForDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await GetInvoicesByConditionAsync("b.Status = 1 AND b.DateCheckOut >= @StartDate AND b.DateCheckOut < @EndDate",
                new SqlParameter("@StartDate", startDate), new SqlParameter("@EndDate", endDate));
        }

        private async Task<DataTable> GetInvoicesBeforeDateAsync(DateTime date)
        {
            return await GetInvoicesByConditionAsync("b.Status = 1 AND b.DateCheckOut < @EndDate",
                new SqlParameter("@EndDate", date));
        }

        private async Task<DataTable> GetInvoicesForMultipleDatesAsync(IEnumerable<DateTime> dates)
        {
            if (!dates.Any()) return new DataTable();

            var dateParams = dates.Select((d, i) => new SqlParameter($"@p{i}", d.Date)).ToList();
            var paramNames = string.Join(", ", dateParams.Select(p => p.ParameterName));
            string condition = $"b.Status = 1 AND CAST(b.DateCheckOut AS DATE) IN ({paramNames})";

            return await GetInvoicesByConditionAsync(condition, dateParams.ToArray());
        }

        private async Task<DataTable> GetInvoicesForCustomerAsync(int customerId)
        {
            return await GetInvoicesByConditionAsync("b.Status = 1 AND b.IdCustomer = @CustomerId",
                new SqlParameter("@CustomerId", customerId));
        }

        // Hàm chung để lấy danh sách hóa đơn dựa trên một điều kiện (condition) và các tham số.
        private async Task<DataTable> GetInvoicesByConditionAsync(string condition, params SqlParameter[] parameters)
        {
            var dt = new DataTable();
            string query = $@"
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
                WHERE {condition}
                ORDER BY b.DateCheckOut DESC";

            using (var connection = new SqlConnection(connectionString))
            {
                var adapter = new SqlDataAdapter(query, connection);
                if (parameters != null && parameters.Length > 0)
                {
                    adapter.SelectCommand.Parameters.AddRange(parameters);
                }

                // Thêm cột 'IsSelected' vào DataTable để binding với CheckBox và đặt giá trị mặc định là true.
                if (!dt.Columns.Contains("IsSelected"))
                {
                    dt.Columns.Add("IsSelected", typeof(bool)).DefaultValue = true;
                }

                await Task.Run(() => adapter.Fill(dt));
            }
            return dt;
        }

        // Tải danh sách khách hàng vào ComboBox.
        private async Task LoadCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name FROM Customer ORDER BY Name";
                var adapter = new SqlDataAdapter(query, connection);
                var customerTable = new DataTable();
                await Task.Run(() => adapter.Fill(customerTable));
                cbCustomers.ItemsSource = customerTable.DefaultView;
            }
        }

        // Tải chi tiết của một hóa đơn để xem trước.
        private async Task<DataView> LoadInvoiceDetailsAsync(int billId)
        {
            const string query = @"
                SELECT d.Name + N' (' + bi.DrinkType + N')' AS DrinkName, bi.Quantity, bi.Price, (bi.Quantity * bi.Price) AS TotalPrice
                FROM BillInfo bi
                JOIN Drink d ON bi.DrinkID = d.ID
                WHERE bi.BillID = @BillID";

            var adapter = new SqlDataAdapter(query, connectionString);
            adapter.SelectCommand.Parameters.AddWithValue("@BillID", billId);
            var detailsTable = new DataTable();
            await Task.Run(() => adapter.Fill(detailsTable));
            return detailsTable.DefaultView;
        }
        #endregion
        private async Task<int> GetInvoiceCountForMultipleDatesAsync(IEnumerable<DateTime> dates)
        {
            if (!dates.Any()) return 0;

            var dateParams = dates.Select((d, i) => new SqlParameter($"@p{i}", d.Date)).ToList();
            var paramNames = string.Join(", ", dateParams.Select(p => p.ParameterName));
            string query = $"SELECT COUNT(ID) FROM Bill WHERE Status = 1 AND CAST(DateCheckOut AS DATE) IN ({paramNames})";

            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(dateParams.ToArray());
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            }
        }

        // Xử lý để cho phép chọn/bỏ chọn ngày trên Calendar bằng cách click chuột.
        private void CalendarMultiSelect_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Tìm control CalendarDayButton (nút ngày) được click.
            if (e.OriginalSource is FrameworkElement originalSource)
            {
                var dayButton = FindVisualParent<CalendarDayButton>(originalSource);
                if (dayButton != null && dayButton.DataContext is DateTime clickedDate)
                {
                    e.Handled = true; // Ngăn chặn hành vi chọn mặc định của Calendar.

                    // Tự quản lý việc chọn/bỏ chọn ngày.
                    if (calendarMultiSelect.SelectedDates.Contains(clickedDate))
                        calendarMultiSelect.SelectedDates.Remove(clickedDate);
                    else
                        calendarMultiSelect.SelectedDates.Add(clickedDate);
                    // Sự kiện SelectedDatesChanged sẽ được kích hoạt sau đó và tự động gọi UpdateSelectedDatesDetailsAsync().
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        // Xử lý khi nhấn nút "Xem" trên một dòng trong DataGrid.
        private async void PreviewInvoice_Click(object sender, RoutedEventArgs e)
        {
            // Lấy dữ liệu của hàng từ CommandParameter của nút.
            if ((sender as Button)?.CommandParameter is DataRowView selectedInvoice)
            {
                int billId = (int)selectedInvoice["ID"];
                var detailsView = await LoadInvoiceDetailsAsync(billId);
                invoicePreview.DisplayInvoice(selectedInvoice, detailsView);
            }
        }
    }
}