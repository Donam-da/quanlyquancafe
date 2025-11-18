using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace namm
{
    // Lớp xử lý logic cho màn hình Chọn khách hàng & Thanh toán (SelectCustomerView.xaml).
    public partial class SelectCustomerView : UserControl
    {
        // Các trường chỉ đọc (readonly) để lưu trữ thông tin được truyền từ màn hình Dashboard.
        private readonly int _tableId; // ID của bàn đang thanh toán.
        private readonly string _tableName; // Tên của bàn đang thanh toán.
        private readonly ObservableCollection<BillItem> _currentBill; // Danh sách các món trong hóa đơn.
        private readonly decimal _totalAmount; // Tổng số tiền của hóa đơn (trước khi giảm giá).
        private readonly AccountDTO _loggedInAccount; // Thông tin tài khoản nhân viên đang đăng nhập.

        // Chuỗi kết nối đến CSDL, được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ và quản lý danh sách khách hàng.
        private DataTable customerDataTable = new DataTable();
        // Lưu trữ thông tin của khách hàng đang được chọn trong DataGrid.
        private DataRowView? _selectedCustomer;

        // Hàm khởi tạo của UserControl, nhận các thông tin cần thiết từ view trước đó.
        public SelectCustomerView(int tableId, string tableName, ObservableCollection<BillItem> currentBill, AccountDTO loggedInAccount)
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.

            // Gán các giá trị được truyền vào cho các trường của lớp.
            _tableId = tableId;
            _loggedInAccount = loggedInAccount;
            _tableName = tableName;
            _currentBill = currentBill;
            // Tính tổng tiền từ danh sách các món trong hóa đơn.
            _totalAmount = _currentBill.Sum(item => item.TotalPrice);

            // Gán sự kiện Loaded để thực thi code sau khi control đã được tải xong hoàn toàn.
            this.Loaded += SelectCustomerView_Loaded;
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private async void SelectCustomerView_Loaded(object sender, RoutedEventArgs e)
        {
            // Hiển thị thông tin hóa đơn lên giao diện.
            tbBillInfo.Text = $"Hóa đơn cho {_tableName} - Tổng cộng: {_totalAmount:N0} VNĐ";
            await LoadCustomersAsync(); // Tải danh sách khách hàng từ CSDL.
            ResetNewCustomerFields(); // Đặt lại form thêm khách hàng về trạng thái ban đầu.
        }

        // Tải danh sách khách hàng cùng với các thông tin thống kê của họ.
        private async Task LoadCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL phức tạp để lấy thông tin khách hàng, số lần mua, tổng chi tiêu và mức giảm giá có thể áp dụng.
                const string query = @"
                    SELECT
                        c.ID,
                        c.Name,
                        c.PhoneNumber,
                        c.Address,
                        c.CustomerCode,
                        COUNT(DISTINCT b.ID) AS PurchaseCount,
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent,
                        -- Subquery để tính mức giảm giá cao nhất mà khách hàng đạt được.
                        -- Nó kiểm tra tất cả các quy tắc giảm giá và lấy ra % cao nhất dựa trên 'Số lần mua' hoặc 'Tổng chi tiêu'.
                        (SELECT FORMAT(ISNULL(MAX(dr.DiscountPercent), 0), 'G29') + '%' 
                         FROM DiscountRule dr 
                         WHERE (dr.CriteriaType = N'Số lần mua' AND COUNT(DISTINCT b.ID) >= dr.Threshold) 
                            OR (dr.CriteriaType = N'Tổng chi tiêu' AND ISNULL(SUM(b.TotalAmount), 0) >= dr.Threshold)) AS DiscountLevel
                    FROM Customer c
                    LEFT JOIN Bill b ON c.ID = b.IdCustomer AND b.Status = 1
                    GROUP BY c.ID, c.Name, c.CustomerCode, c.PhoneNumber, c.Address
                    ORDER BY c.Name;
                ";
                var adapter = new SqlDataAdapter(query, connection);
                customerDataTable = new DataTable();
                customerDataTable.Columns.Add("STT", typeof(int)); // Thêm cột STT để đánh số thứ tự trên giao diện.

                // Chạy tác vụ lấy dữ liệu trên một luồng nền để không làm treo giao diện.
                await Task.Run(() => adapter.Fill(customerDataTable));

                dgCustomers.ItemsSource = customerDataTable.DefaultView; // Gán dữ liệu cho DataGrid.
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải, dùng để đánh số thứ tự.
        private void DgCustomers_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1; // Gán số thứ tự.
            }
        }

        // Sự kiện được gọi khi người dùng chọn một khách hàng trong DataGrid.
        private void DgCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nếu có một khách hàng được chọn.
            if (dgCustomers.SelectedItem is DataRowView selected)
            {
                _selectedCustomer = selected; // Lưu lại thông tin khách hàng đã chọn.
                // Hiển thị thông tin tóm tắt của khách hàng đã chọn.
                tbSelectedCustomer.Text = $"Đã chọn: {selected["Name"]} (Mã: {selected["CustomerCode"]}) - SĐT: {selected["PhoneNumber"]}";
                btnPay.IsEnabled = true; // Bật nút "Thanh toán".

                // Điền thông tin của khách hàng đã chọn vào form bên phải để người dùng có thể sửa.
                txtNewCustomerName.Text = selected["Name"].ToString();
                txtNewCustomerPhone.Text = selected["PhoneNumber"].ToString();
                txtNewCustomerCode.Text = selected["CustomerCode"].ToString();
                txtNewCustomerAddress.Text = selected["Address"].ToString();

                // Cập nhật trạng thái các nút: tắt nút Thêm, bật nút Sửa/Xóa.
                btnAddNewCustomer.IsEnabled = false;
                btnEditCustomer.IsEnabled = true;
                btnDeleteCustomer.IsEnabled = true;
            }
            else
            {
                _selectedCustomer = null;
                tbSelectedCustomer.Text = "(Chưa chọn khách hàng)";
                btnPay.IsEnabled = false; // Tắt nút "Thanh toán".
                // Các nút Sửa/Xóa sẽ được xử lý trong hàm ResetNewCustomerFields().
            }
        }

        // Áp dụng bộ lọc cho DataGrid dựa trên nội dung ô tìm kiếm.
        private void ApplyCustomerFilter()
        {
            string filter = txtSearchCustomer.Text.Replace("'", "''"); // Thay thế ký tự ' để tránh lỗi cú pháp trong RowFilter.
            if (customerDataTable.DefaultView != null)
            {
                // Lọc trên các cột Name, PhoneNumber, hoặc CustomerCode.
                customerDataTable.DefaultView.RowFilter =
                    $"Name LIKE '%{filter}%' OR PhoneNumber LIKE '%{filter}%' OR CustomerCode LIKE '%{filter}%'";
            }
        }

        // Sự kiện được gọi khi người dùng nhấn phím trong ô tìm kiếm.
        private void TxtSearchCustomer_KeyDown(object sender, KeyEventArgs e)
        {
            // Chỉ áp dụng bộ lọc khi người dùng nhấn Enter để tránh việc lọc liên tục gây giật lag.
            if (e.Key == Key.Enter)
            {   
                ApplyCustomerFilter();
            }
        }

        // Sự kiện được gọi khi người dùng rời khỏi ô nhập tên (LostFocus), dùng để tự động tạo mã khách hàng.
        private async void TxtNewCustomerName_LostFocus(object sender, RoutedEventArgs e)
        {
            // Chỉ tạo mã mới khi đang ở chế độ "Thêm mới"
            if (btnAddNewCustomer.IsEnabled)
            {
                string baseCode = GenerateCustomerCode(txtNewCustomerName.Text);
                if (string.IsNullOrEmpty(baseCode))
                {
                    txtNewCustomerCode.Text = string.Empty;
                    return;
                }

                // Tìm một mã khách hàng duy nhất dựa trên tên.
                string uniqueCode = await GetNextAvailableCustomerCodeAsync(baseCode);
                txtNewCustomerCode.Text = uniqueCode;
            }
        }

        // Hàm tạo mã khách hàng cơ bản từ tên (chuyển thành chữ thường, không dấu, không ký tự đặc biệt).
        private string GenerateCustomerCode(string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
                return string.Empty;

            string temp = customerName.ToLower().Trim();

            // Bỏ dấu tiếng Việt bằng cách thay thế các ký tự có dấu bằng ký tự không dấu tương ứng.
            temp = Regex.Replace(temp, "[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            temp = Regex.Replace(temp, "[éèẻẽẹêếềểễệ]", "e");
            temp = Regex.Replace(temp, "[íìỉĩị]", "i");
            temp = Regex.Replace(temp, "[óòỏõọôốồổỗộơớờởỡợ]", "o");
            temp = Regex.Replace(temp, "[úùủũụưứừửữự]", "u");
            temp = Regex.Replace(temp, "[ýỳỷỹỵ]", "y");
            temp = Regex.Replace(temp, "[đ]", "d");

            // Thay thế một hoặc nhiều khoảng trắng bằng một dấu gạch dưới.
            temp = Regex.Replace(temp, @"\s+", "_");

            // Loại bỏ tất cả các ký tự không phải là chữ cái (a-z), số (0-9), hoặc dấu gạch dưới.
            temp = Regex.Replace(temp, "[^a-z0-9_]", "");

            return temp;
        }

        // Hàm kiểm tra và tìm mã khách hàng duy nhất. Nếu mã đã tồn tại, nó sẽ thêm hậu tố số (ví dụ: _1, _2).
        private async Task<string> GetNextAvailableCustomerCodeAsync(string baseCode)
        {
            var existingCodes = new HashSet<string>();
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand("SELECT CustomerCode FROM Customer WHERE CustomerCode LIKE @Pattern", connection);
                command.Parameters.AddWithValue("@Pattern", baseCode + "%");

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingCodes.Add(reader.GetString(0));
                    }
                }
            }

            // Nếu mã cơ bản chưa tồn tại, trả về chính nó.
            if (!existingCodes.Contains(baseCode)) return baseCode;

            // Nếu đã tồn tại, tìm hậu tố số tiếp theo.
            int suffix = 1;
            while (existingCodes.Contains($"{baseCode}_{suffix}")) { suffix++; }
            return $"{baseCode}_{suffix}";
        }

        // Xử lý sự kiện khi nhấn nút "Thêm mới".
        private async void BtnAddNewCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewCustomerName.Text))
            {
                MessageBox.Show("Tên khách hàng không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNewCustomerCode.Text))
            {
                MessageBox.Show("Mã khách hàng không được để trống. Vui lòng nhập tên và rời khỏi ô để tạo mã.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO Customer (Name, CustomerCode, PhoneNumber, Address) OUTPUT INSERTED.ID VALUES (@Name, @CustomerCode, @PhoneNumber, @Address)";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Name", txtNewCustomerName.Text);
                command.Parameters.AddWithValue("@CustomerCode", txtNewCustomerCode.Text);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(txtNewCustomerPhone.Text) ? DBNull.Value : (object)txtNewCustomerPhone.Text);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(txtNewCustomerAddress.Text) ? DBNull.Value : (object)txtNewCustomerAddress.Text);

                try
                {
                    await connection.OpenAsync();
                    var newCustomerId = (int)await command.ExecuteScalarAsync(); // Lấy ID của khách hàng vừa được tạo.

                    MessageBox.Show("Thêm khách hàng mới thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCustomersAsync(); // Tải lại danh sách khách hàng.
                    ResetNewCustomerFields(); // Làm mới form.

                    // Tự động chọn khách hàng vừa thêm trong DataGrid.
                    dgCustomers.SelectedValue = newCustomerId; 
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm khách hàng: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Lưu sửa".
        private async void BtnEditCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNewCustomerName.Text))
            {
                MessageBox.Show("Tên khách hàng không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Cảnh báo người dùng rằng việc thay đổi tên sẽ không thay đổi mã khách hàng.
            if (txtNewCustomerName.Text != _selectedCustomer["Name"].ToString())
            {
                if (MessageBox.Show("Bạn đã thay đổi tên khách hàng. Điều này sẽ không thay đổi mã khách hàng hiện tại. Bạn có muốn tiếp tục?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            int customerId = (int)_selectedCustomer["ID"];

            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "UPDATE Customer SET Name = @Name, PhoneNumber = @PhoneNumber, Address = @Address WHERE ID = @ID"; // Không cho phép cập nhật CustomerCode.
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", customerId);
                command.Parameters.AddWithValue("@Name", txtNewCustomerName.Text);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(txtNewCustomerPhone.Text) ? DBNull.Value : (object)txtNewCustomerPhone.Text);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(txtNewCustomerAddress.Text) ? DBNull.Value : (object)txtNewCustomerAddress.Text);

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Cập nhật thông tin khách hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCustomersAsync(); // Tải lại danh sách.
                    dgCustomers.SelectedValue = customerId; // Chọn lại khách hàng vừa sửa
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật khách hàng: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thanh toán" cho khách hàng thành viên.
        private async void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            int? customerId = null;
            if (_selectedCustomer != null)
            {
                customerId = (int)_selectedCustomer["ID"];
                string customerName = _selectedCustomer["Name"].ToString();
                string customerCode = _selectedCustomer["CustomerCode"].ToString();

                // Lấy thông tin thống kê (số lần mua, tổng chi) của khách hàng.
                var customerStats = await GetCustomerStatsAsync(customerId.Value);

                // Lấy tất cả các quy tắc giảm giá và tính toán mức giảm giá cao nhất có thể áp dụng.
                var allRules = await GetDiscountRulesAsync();
                decimal discountPercent = CalculateDiscount(customerStats, allRules);

                decimal finalAmount = _totalAmount * (1 - (discountPercent / 100m)); // Tính tổng tiền cuối cùng sau khi giảm giá.

                // Lấy ID của hóa đơn chưa thanh toán (status=0) cho bàn hiện tại.
                int billId = await GetUnpaidBillIdForTableAsync(_tableId);
                if (billId == -1)
                {
                    MessageBox.Show("Không tìm thấy hóa đơn chưa thanh toán cho bàn này.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Tạo và hiển thị cửa sổ hóa đơn (InvoiceWindow) để xem trước và xác nhận.
                var invoiceWindow = new InvoiceWindow(_tableId, _tableName, customerName, customerCode, _totalAmount, discountPercent, finalAmount, _currentBill, billId);
                invoiceWindow.Owner = Window.GetWindow(this);

                // Chỉ xử lý thanh toán khi người dùng nhấn "Xác nhận" trên hóa đơn
                if (invoiceWindow.ShowDialog() == true)
                {
                    // Lưu hóa đơn với tổng tiền đã giảm giá
                    await ProcessPaymentAsync(customerId, null, finalAmount); // Gọi hàm xử lý thanh toán.
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thanh toán (Khách vãng lai)".
        private async void BtnPayAsGuest_Click(object sender, RoutedEventArgs e)
        {
            string customerName = "Khách vãng lai";
            // Tạo một mã khách hàng vãng lai duy nhất dựa trên thời gian hiện tại.
            string customerCode = DateTime.Now.ToString("HHmmssddMMyy");

            int billId = await GetUnpaidBillIdForTableAsync(_tableId);
            if (billId == -1)
            {
                MessageBox.Show("Không tìm thấy hóa đơn chưa thanh toán cho bàn này.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Khách vãng lai không có giảm giá (0%), tổng tiền cuối cùng bằng tổng tiền hàng.
            var invoiceWindow = new InvoiceWindow(_tableId, _tableName, customerName, customerCode, _totalAmount, 0, _totalAmount, _currentBill, billId);
            invoiceWindow.Owner = Window.GetWindow(this);

            // Nếu người dùng xác nhận trên cửa sổ hóa đơn.
            if (invoiceWindow.ShowDialog() == true)
            {
                // Xử lý thanh toán, truyền vào mã khách vãng lai.
                await ProcessPaymentAsync(null, customerCode, _totalAmount);
            }
        }

        // Hàm lấy ID của hóa đơn chưa thanh toán (Status = 0) cho một bàn cụ thể.
        private async Task<int> GetUnpaidBillIdForTableAsync(int tableId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand("SELECT ID FROM Bill WHERE TableID = @TableID AND Status = 0", connection);
                command.Parameters.AddWithValue("@TableID", tableId);
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return result != null ? (int)result : -1;
            }
        }

        // Hàm xử lý chính cho việc thanh toán: cập nhật hóa đơn, cập nhật trạng thái bàn.
        private async Task ProcessPaymentAsync(int? customerId, string? guestCustomerCode, decimal finalAmount)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // Câu lệnh cập nhật hóa đơn: đổi Status thành 1 (đã thanh toán), ghi lại ngày giờ, thông tin khách hàng, nhân viên và số tiền.
                const string updateBillQuery = @"
                    UPDATE Bill SET 
                        Status = 1, -- Đã thanh toán
                        DateCheckOut = GETDATE(),
                        IdCustomer = @CustomerID,
                        GuestCustomerCode = @GuestCustomerCode,
                        AccountUserName = @AccountUserName,
                        -- Lưu cả tổng tiền gốc (SubTotal) và tổng tiền sau giảm giá (TotalAmount).
                        SubTotal = @SubTotal,
                        TotalAmount = @FinalAmount
                    WHERE TableID = @TableID AND Status = 0";

                var updateBillCmd = new SqlCommand(updateBillQuery, connection);
                updateBillCmd.Parameters.AddWithValue("@CustomerID", customerId ?? (object)DBNull.Value);
                updateBillCmd.Parameters.AddWithValue("@GuestCustomerCode", string.IsNullOrEmpty(guestCustomerCode) ? (object)DBNull.Value : guestCustomerCode);
                updateBillCmd.Parameters.AddWithValue("@AccountUserName", _loggedInAccount.UserName);
                updateBillCmd.Parameters.AddWithValue("@FinalAmount", finalAmount);
                updateBillCmd.Parameters.AddWithValue("@SubTotal", _totalAmount); // _totalAmount là tổng tiền gốc
                updateBillCmd.Parameters.AddWithValue("@TableID", _tableId);

                // Câu lệnh cập nhật trạng thái bàn về 'Trống'.
                const string updateTableQuery = "UPDATE TableFood SET Status = N'Trống' WHERE ID = @TableID";
                var updateTableCmd = new SqlCommand(updateTableQuery, connection);
                updateTableCmd.Parameters.AddWithValue("@TableID", _tableId);

                await updateBillCmd.ExecuteNonQueryAsync();
                await updateTableCmd.ExecuteNonQueryAsync();
            }

            MessageBox.Show("Thanh toán thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            NavigateBackToDashboard(); // Quay trở lại màn hình chính.
        }

        // Hàm lấy thông tin thống kê (số lần mua, tổng chi) của một khách hàng.
        private async Task<(int PurchaseCount, decimal TotalSpent)> GetCustomerStatsAsync(int customerId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT
                        COUNT(b.ID) AS PurchaseCount,
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent
                    FROM Bill b
                    WHERE b.IdCustomer = @CustomerId AND b.Status = 1"; // Chỉ tính các hóa đơn đã thanh toán
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CustomerId", customerId);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return (reader.GetInt32(0), reader.GetDecimal(1));
                    }
                }
            }
            return (0, 0); // Trả về mặc định nếu không có dữ liệu
        }

        // Hàm lấy tất cả các quy tắc giảm giá từ CSDL.
        private async Task<List<DiscountRule>> GetDiscountRulesAsync()
        {
            var rules = new List<DiscountRule>();
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT CriteriaType, Threshold, DiscountPercent FROM DiscountRule";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rules.Add(new DiscountRule
                        {
                            CriteriaType = reader.GetString(0),
                            Threshold = reader.GetDecimal(1),
                            DiscountPercent = reader.GetDecimal(2)
                        });
                    }
                }
            }
            return rules;
        }

        // Hàm tính toán mức giảm giá cao nhất có thể áp dụng cho khách hàng.
        private decimal CalculateDiscount((int PurchaseCount, decimal TotalSpent) stats, List<DiscountRule> rules)
        {
            // Lọc ra tất cả các quy tắc mà khách hàng thỏa mãn điều kiện.
            var applicableRules = rules.Where(rule =>
                (rule.CriteriaType == "Số lần mua" && stats.PurchaseCount >= rule.Threshold) ||
                (rule.CriteriaType == "Tổng chi tiêu" && stats.TotalSpent >= rule.Threshold)
            );

            // Nếu có bất kỳ quy tắc nào áp dụng được, tìm ra mức giảm giá cao nhất trong số đó.
            if (applicableRules.Any())
            {
                return applicableRules.Max(rule => rule.DiscountPercent);
            }

            // Nếu không có quy tắc nào thỏa mãn, trả về 0.
            return 0;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigateBackToDashboard();
        }

        // Hàm xóa trắng các ô nhập liệu và đặt lại trạng thái các nút về chế độ "Thêm mới".
        private void ResetNewCustomerFields()
        {
            txtNewCustomerName.Clear();
            txtNewCustomerPhone.Clear();
            txtNewCustomerCode.Clear();
            txtNewCustomerAddress.Clear();
            dgCustomers.SelectedItem = null;

            // Đặt lại trạng thái các nút về ban đầu.
            btnAddNewCustomer.IsEnabled = true;
            btnEditCustomer.IsEnabled = false;
            btnDeleteCustomer.IsEnabled = false;
        }

        // Hàm điều hướng quay trở lại màn hình Dashboard.
        private void NavigateBackToDashboard()
        {
            var mainAppWindow = Window.GetWindow(this) as MainAppWindow;
            if (mainAppWindow != null)
            {
                mainAppWindow.MainContent.Children.Clear();
                mainAppWindow.MainContent.Children.Add(new DashboardView(mainAppWindow.LoggedInAccount));
            }
        }

        // Xử lý sự kiện khi nhấn nút "Làm mới".
        private void BtnResetNewCustomer_Click(object sender, RoutedEventArgs e)
        {
            ResetNewCustomerFields();
        }

        // Xử lý sự kiện khi nhấn nút "Xóa" khách hàng.
        private async void BtnDeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string customerName = _selectedCustomer["Name"].ToString();
            if (MessageBox.Show($"Bạn có chắc chắn muốn xóa khách hàng '{customerName}' không? Hành động này không thể hoàn tác.",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            int customerId = (int)_selectedCustomer["ID"];

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Kiểm tra xem khách hàng này đã có lịch sử giao dịch chưa.
                var checkBillCmd = new SqlCommand("SELECT COUNT(1) FROM Bill WHERE IdCustomer = @CustomerId", connection);
                checkBillCmd.Parameters.AddWithValue("@CustomerId", customerId);
                int billCount = (int)await checkBillCmd.ExecuteScalarAsync();

                if (billCount > 0)
                {
                    // Nếu đã có giao dịch, không cho phép xóa để bảo toàn dữ liệu.
                    MessageBox.Show($"Không thể xóa khách hàng '{customerName}' vì họ đã có lịch sử giao dịch.",
                        "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var deleteCmd = new SqlCommand("DELETE FROM Customer WHERE ID = @ID", connection);
                deleteCmd.Parameters.AddWithValue("@ID", customerId);

                try
                {
                    int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Xóa khách hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadCustomersAsync(); // Tải lại danh sách.
                    }
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi xóa khách hàng: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
