﻿﻿// File này chứa logic xử lý cho LoyalCustomerView.xaml.
// Chức năng chính bao gồm:
// 1. Tải và hiển thị danh sách khách hàng cùng với thống kê chi tiêu và số lần mua hàng.
// 2. Quản lý (thêm, xóa, tải) các quy tắc giảm giá (DiscountRule) từ cơ sở dữ liệu.
// 3. Tự động tính toán và hiển thị mức giảm giá mà mỗi khách hàng có thể đạt được.
// 4. Cho phép xóa khách hàng khỏi hệ thống (với điều kiện họ chưa có lịch sử giao dịch).
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.CompilerServices;

namespace namm
{
    // Lớp đại diện cho một quy tắc giảm giá.
    public class DiscountRule : INotifyPropertyChanged
    {
        public int ID { get; set; } // ID của quy tắc trong CSDL.
        public string CriteriaType { get; set; } = string.Empty; // Tiêu chí áp dụng (ví dụ: "Số lần mua", "Tổng chi tiêu").
        public decimal Threshold { get; set; } // Ngưỡng cần đạt để được giảm giá.
        public decimal DiscountPercent { get; set; } // Phần trăm giảm giá.

        // INotifyPropertyChanged được triển khai nhưng không thực sự cần thiết trong logic hiện tại,
        // vì dữ liệu được tải lại hoàn toàn thay vì cập nhật từng thuộc tính.
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Lớp chính cho UserControl, xử lý tất cả các sự kiện và logic.
    public partial class LoyalCustomerView : UserControl
    {
        // Chuỗi kết nối CSDL được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // DataTable để lưu trữ dữ liệu khách hàng tải từ CSDL. Dùng DataTable thay vì ObservableCollection<Customer>
        // cho phép thêm cột "STT" và "Discount" một cách linh hoạt.
        private DataTable customerTable = new DataTable();
        // ObservableCollection để lưu các quy tắc giảm giá. Khi thêm/xóa item, ListView sẽ tự động cập nhật.
        private ObservableCollection<DiscountRule> discountRules = new ObservableCollection<DiscountRule>();

        public LoyalCustomerView()
        {
            InitializeComponent();
        }

        // Sự kiện được gọi khi UserControl được tải lần đầu.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                lvDiscountRules.ItemsSource = discountRules; // Gán collection cho ListView.
                await LoadDiscountRulesAsync(); // Tải các quy tắc giảm giá từ CSDL.
                await LoadLoyalCustomersAsync(); // Tải danh sách khách hàng và thống kê của họ.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đã xảy ra lỗi khi tải dữ liệu khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Phương thức bất đồng bộ để tải dữ liệu khách hàng.
        private async Task LoadLoyalCustomersAsync()
        {
            // Sử dụng 'using' để đảm bảo kết nối được đóng đúng cách.
            using (var connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL phức tạp để lấy thông tin khách hàng và thống kê liên quan.
                const string query = @"
                    SELECT
                        c.ID,
                        c.Name AS CustomerName,
                        c.CustomerCode,
                        c.PhoneNumber,
                        c.Address,
                        COUNT(DISTINCT b.ID) AS PurchaseCount, -- Đếm số hóa đơn đã thanh toán (Status=1) để lấy số lần mua.
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent, -- Tính tổng số tiền đã chi tiêu.
                        ISNULL(SUM(b.SubTotal - b.TotalAmount), 0) AS TotalDiscountGiven, -- Tính tổng số tiền đã được giảm giá.
                        -- Logic quan trọng: Tìm mức giảm giá cao nhất mà khách hàng có thể đạt được dựa trên các quy tắc hiện có.
                        -- Nó kiểm tra cả hai tiêu chí (Số lần mua và Tổng chi tiêu) và lấy ra % giảm giá lớn nhất.
                        (SELECT FORMAT(ISNULL(MAX(dr.DiscountPercent), 0), 'G29') + '%' 
                         FROM DiscountRule dr 
                         WHERE (dr.CriteriaType = N'Số lần mua' AND COUNT(DISTINCT b.ID) >= dr.Threshold) 
                            OR (dr.CriteriaType = N'Tổng chi tiêu' AND ISNULL(SUM(b.TotalAmount), 0) >= dr.Threshold)) AS AppliedRuleDescription
                    FROM Customer c
                    LEFT JOIN Bill b ON c.ID = b.IdCustomer AND b.Status = 1 -- Chỉ join với các hóa đơn đã thanh toán.
                    GROUP BY c.ID, c.Name, c.CustomerCode, c.PhoneNumber, c.Address -- Nhóm theo thông tin khách hàng.
                    ORDER BY TotalSpent DESC; -- Sắp xếp theo tổng chi tiêu giảm dần.
                ";

                var adapter = new SqlDataAdapter(query, connection);
                customerTable = new DataTable();
                customerTable.Columns.Add("STT", typeof(int)); // Thêm cột STT vào DataTable.
                customerTable.Columns.Add("Discount", typeof(decimal)); // Thêm cột để lưu tổng tiền đã được giảm.

                // Chạy câu lệnh và điền dữ liệu vào DataTable một cách bất đồng bộ.
                await Task.Run(() => adapter.Fill(customerTable));

                // Gán giá trị cho cột "Discount" từ cột "TotalDiscountGiven" đã lấy từ CSDL.
                // Cột STT sẽ được gán giá trị trong sự kiện LoadingRow của DataGrid.
                for (int i = 0; i < customerTable.Rows.Count; i++)
                {
                    customerTable.Rows[i]["Discount"] = customerTable.Rows[i]["TotalDiscountGiven"];
                }

                // Gán dữ liệu từ DataTable cho DataGrid để hiển thị.
                dgLoyalCustomers.ItemsSource = customerTable.DefaultView;
            }
        }

        // Phương thức bất đồng bộ để tải các quy tắc giảm giá từ CSDL.
        private async Task LoadDiscountRulesAsync()
        {
            discountRules.Clear(); // Xóa các quy tắc cũ trước khi tải lại.
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, CriteriaType, Threshold, DiscountPercent FROM DiscountRule ORDER BY CriteriaType, Threshold";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Đọc từng dòng kết quả và tạo đối tượng DiscountRule.
                    while (await reader.ReadAsync())
                    {
                        discountRules.Add(new DiscountRule
                        {
                            ID = reader.GetInt32(0), // Cột ID
                            CriteriaType = reader.GetString(1), // Cột CriteriaType
                            Threshold = reader.GetDecimal(2), // Cột Threshold
                            DiscountPercent = reader.GetDecimal(3) // Cột DiscountPercent
                        });
                    }
                }
            }
        }

        // Xử lý sự kiện khi người dùng nhấn nút "Thêm mức".
        private async void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra dữ liệu đầu vào.
            if (cbCriteriaType.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một tiêu chí.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtThreshold.Text, out decimal threshold) || threshold <= 0) // Ngưỡng phải là số dương.
            {
                MessageBox.Show("Ngưỡng phải là một số dương hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtDiscountPercent.Text, out decimal discountPercent) || discountPercent < 0) // % giảm giá không được âm.
            {
                MessageBox.Show("Mức giảm giá phải là một số không âm hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string criteriaType = ((ComboBoxItem)cbCriteriaType.SelectedItem).Content.ToString() ?? "";

            // Lưu quy tắc mới vào CSDL.
            using (var connection = new SqlConnection(connectionString))
            {
                // Sử dụng "OUTPUT INSERTED.ID" để lấy lại ID vừa được tạo tự động.
                const string query = "INSERT INTO DiscountRule (CriteriaType, Threshold, DiscountPercent) OUTPUT INSERTED.ID VALUES (@CriteriaType, @Threshold, @DiscountPercent)";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CriteriaType", criteriaType);
                command.Parameters.AddWithValue("@Threshold", threshold);
                command.Parameters.AddWithValue("@DiscountPercent", discountPercent);

                try
                {
                    await connection.OpenAsync();
                    int newId = (int)(await command.ExecuteScalarAsync() ?? 0);

                    // Thêm quy tắc mới vào ObservableCollection để cập nhật UI.
                    discountRules.Add(new DiscountRule
                    {
                        ID = newId,
                        CriteriaType = criteriaType,
                        Threshold = threshold,
                        DiscountPercent = discountPercent
                    });

                    // Xóa trắng các ô nhập liệu để chuẩn bị cho lần nhập tiếp theo.
                    cbCriteriaType.SelectedIndex = -1;
                    txtThreshold.Clear();
                    txtDiscountPercent.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thêm mức giảm giá: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Xử lý sự kiện khi người dùng nhấn nút "Xóa mức đã chọn".
        private async void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            // Lấy tất cả các quy tắc đang được chọn trong ListView.
            var selectedRules = lvDiscountRules.SelectedItems.Cast<DiscountRule>().ToList();

            if (selectedRules.Any())
            {
                // Hỏi xác nhận trước khi xóa.
                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa {selectedRules.Count} mức giảm giá đã chọn không?", 
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }

                var idsToDelete = selectedRules.Select(r => r.ID).ToList(); // Lấy danh sách ID cần xóa.
                string idList = string.Join(",", idsToDelete); // Tạo chuỗi ID để dùng trong câu lệnh SQL.

                // Xóa các quy tắc khỏi CSDL.
                using (var connection = new SqlConnection(connectionString))
                {
                    // Sử dụng mệnh đề IN để xóa nhiều bản ghi cùng lúc.
                    var command = new SqlCommand($"DELETE FROM DiscountRule WHERE ID IN ({idList})", connection);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }

                // Xóa các quy tắc khỏi ObservableCollection để cập nhật UI.
                foreach (var rule in selectedRules)
                {
                    discountRules.Remove(rule);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một mức giảm giá để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Sự kiện được gọi cho mỗi dòng khi DataGrid đang được tải.
        private void DgLoyalCustomers_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Gán lại số thứ tự dựa trên vị trí hiển thị của hàng trong DataGrid.
            // Điều này đảm bảo STT luôn đúng thứ tự 1, 2, 3,... ngay cả khi sắp xếp.
            if (e.Row.Item is DataRowView rowView)
            {
                rowView.Row["STT"] = e.Row.GetIndex() + 1;
            }
        }

        // Xử lý sự kiện khi nhấn nút "Áp dụng cho tất cả KH".
        private async void BtnApplyRuleToAll_Click(object sender, RoutedEventArgs e)
        {
            var selectedRules = lvDiscountRules.SelectedItems.Cast<DiscountRule>().ToList();

            string ruleDescription = $"{selectedRules.Count} mức";
            if (MessageBox.Show($"Hành động này sẽ áp dụng mức '{ruleDescription}' cho TẤT CẢ khách hàng trong hệ thống. Bạn có chắc chắn muốn tiếp tục?",
                "CẢNH BÁO", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            // Logic hiện tại không cần áp dụng quy tắc cứng vào CSDL, vì nó được tính toán động.
            // Do đó, hàm này chỉ tải lại dữ liệu để đảm bảo hiển thị là mới nhất.
            var allCustomerIds = customerTable.AsEnumerable().Select(row => row.Field<int>("ID")).ToList();
            await ApplyRulesToCustomers(allCustomerIds, selectedRules.Select(r => r.ID).ToList());
        }

        private void LvDiscountRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Bật/tắt nút "Xóa mức" dựa trên việc có quy tắc nào được chọn hay không.
            bool anyRuleSelected = lvDiscountRules.SelectedItems.Count > 0;
            btnRemoveRule.IsEnabled = anyRuleSelected;
        }

        private void RuleHeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox headerCheckBox)
            {
                // Khi nhấn checkbox ở header, chọn hoặc bỏ chọn tất cả các item trong ListView.
                bool isChecked = headerCheckBox.IsChecked ?? false;
                foreach (var item in lvDiscountRules.Items)
                {
                    if (lvDiscountRules.ItemContainerGenerator.ContainerFromItem(item) is ListViewItem lvi)
                        lvi.IsSelected = isChecked;
                }
            }
        }

        // Phương thức này hiện chỉ làm mới lại danh sách khách hàng.
        private async Task ApplyRulesToCustomers(List<int> customerIds, List<int> ruleIds)
        {
            // Trong thiết kế hiện tại, các quy tắc được áp dụng động khi thanh toán,
            // vì vậy không cần cập nhật trực tiếp CSDL ở đây để liên kết quy tắc với khách hàng.
            // Hành động này có thể được coi là một "làm mới" để đảm bảo chế độ xem được cập nhật.
            // Chúng ta chỉ cần hiển thị một thông báo thành công và tải lại dữ liệu khách hàng.
            MessageBox.Show($"Thao tác thành công. Dữ liệu khách hàng sẽ được làm mới.",
                            "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadLoyalCustomersAsync();
        }

        // Handler này được định nghĩa trong XAML nhưng không có logic bên trong.
        // Nó có thể là một placeholder hoặc một phần của logic cũ.
        private void DataGridRow_PreviewMouseLeftButtonDown_Selection(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

        // Handler cho checkbox ở header của DataGrid khách hàng (hiện không có trong XAML).
        // Logic này là một placeholder.
        private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked ?? false;
            if (dgLoyalCustomers.ItemsSource is DataView dataView)
            {
                // Phần logic này dường như bị thiếu hoặc không chính xác trong mã gốc.
                // Đây là một placeholder để chọn tất cả khách hàng.
            }
        }

        // Xử lý sự kiện khi nhấn nút "Xóa khách hàng đã chọn".
        private async void BtnDeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (dgLoyalCustomers.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCustomerRow = (DataRowView)dgLoyalCustomers.SelectedItem; // Lấy dòng khách hàng được chọn.
            string? customerName = selectedCustomerRow["CustomerName"].ToString();
            int customerId = (int)selectedCustomerRow["ID"]; // Lấy ID của khách hàng.

            // Hỏi xác nhận trước khi xóa.
            if (MessageBox.Show($"Bạn có chắc chắn muốn xóa khách hàng '{customerName}' không? Hành động này KHÔNG THỂ hoàn tác.",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Bước 1: Kiểm tra xem khách hàng có lịch sử giao dịch không.
                var checkBillCmd = new SqlCommand("SELECT COUNT(1) FROM Bill WHERE IdCustomer = @CustomerId", connection);
                checkBillCmd.Parameters.AddWithValue("@CustomerId", customerId);
                int billCount = (int)(await checkBillCmd.ExecuteScalarAsync() ?? 0);

                if (billCount > 0)
                {
                    // Nếu có, không cho phép xóa để bảo toàn dữ liệu lịch sử.
                    MessageBox.Show($"Không thể xóa khách hàng '{customerName}' vì họ đã có lịch sử giao dịch.",
                        "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Bước 2: Nếu không có giao dịch, tiến hành xóa khách hàng khỏi bảng Customer.
                var deleteCmd = new SqlCommand("DELETE FROM Customer WHERE ID = @ID", connection);
                deleteCmd.Parameters.AddWithValue("@ID", customerId);

                int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    MessageBox.Show("Xóa khách hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadLoyalCustomersAsync(); // Tải lại danh sách khách hàng để cập nhật UI.
                }
            }
        }
    }
}