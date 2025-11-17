using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    // Lớp xử lý logic cho màn hình Thống kê Doanh thu theo Nhân viên.
    public partial class EmployeeRevenueView : UserControl
    {
        // Chuỗi kết nối CSDL.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu để lưu trữ kết quả thống kê.
        private DataTable revenueDataTable = new DataTable();
        // Biến để lưu thông tin tài khoản đang đăng nhập.
        private AccountDTO? loggedInAccount;

        // Hàm khởi tạo, nhận thông tin tài khoản đăng nhập để phân quyền.
        public EmployeeRevenueView(AccountDTO? account = null)
        {
            InitializeComponent();
            this.loggedInAccount = account;
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Tạm gỡ các event handler để tránh việc lọc dữ liệu bị gọi nhiều lần trong lúc thiết lập ban đầu.
            dpStartDate.SelectedDateChanged -= Filters_Changed;
            dpEndDate.SelectedDateChanged -= Filters_Changed;
            cbEmployees.SelectionChanged -= Filters_Changed;

            await LoadEmployeeFilterAsync(); // Tải danh sách nhân viên vào ComboBox.

            // Thiết lập khoảng thời gian lọc mặc định là từ đầu tháng đến ngày hiện tại.
            var today = DateTime.Today;
            dpStartDate.SelectedDate = new DateTime(today.Year, today.Month, 1);
            dpEndDate.SelectedDate = today;

            // Gắn lại các event handler sau khi đã thiết lập xong.
            dpStartDate.SelectedDateChanged += Filters_Changed;
            dpEndDate.SelectedDateChanged += Filters_Changed;
            cbEmployees.SelectionChanged += Filters_Changed;

            ApplyAuthorization(); // Áp dụng phân quyền (ví dụ: nhân viên chỉ xem được của mình).
            await FilterData(); // Tải dữ liệu lần đầu với bộ lọc mặc định.
        }

        // Tải danh sách nhân viên vào ComboBox lọc.
        private async Task LoadEmployeeFilterAsync()
        {
            var employeeTable = new DataTable();
            using (var connection = new SqlConnection(connectionString))
            {
                string query;
                // Nếu là Admin (Type=1), tải tất cả nhân viên và thêm mục "Tất cả".
                if (loggedInAccount?.Type == 1)
                {
                    query = @"
                        SELECT 'ALL_USERS' AS UserName, N'Tất cả' AS DisplayName, -1 AS SortOrder
                        UNION ALL
                        SELECT UserName, DisplayName, 0 AS SortOrder FROM Account WHERE Type IN (0, 1)
                        ORDER BY SortOrder, DisplayName";
                }
                else // Nếu là nhân viên (Type=0), chỉ tải chính mình.
                {
                    query = "SELECT UserName, DisplayName FROM Account WHERE UserName = @UserName";
                }

                var adapter = new SqlDataAdapter(query, connection);
                // Nếu là nhân viên, thêm tham số UserName vào câu lệnh.
                if (loggedInAccount?.Type == 0)
                {
                    adapter.SelectCommand.Parameters.AddWithValue("@UserName", loggedInAccount.UserName);
                }

                await Task.Run(() => adapter.Fill(employeeTable));
            }
            cbEmployees.ItemsSource = employeeTable.DefaultView; // Gán dữ liệu cho ComboBox.
            cbEmployees.SelectedIndex = 0;
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            await FilterData();
        }

        // Sự kiện được gọi mỗi khi một bộ lọc thay đổi (ngày hoặc nhân viên).
        private async void Filters_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded) return;
            await FilterData();
        }

        // Hàm trung tâm để thu thập các giá trị từ bộ lọc và gọi hàm tải dữ liệu.
        private async Task FilterData()
        {
            DateTime? startDate = dpStartDate.SelectedDate?.Date;
            DateTime? endDate = dpEndDate.SelectedDate?.Date.AddDays(1).AddTicks(-1); // Lấy đến cuối ngày
            string? employeeUserName = null;

            // Nếu là nhân viên, luôn lấy username của họ.
            if (loggedInAccount?.Type == 0)
            {
                employeeUserName = loggedInAccount.UserName;
            }
            else // Nếu là Admin, lấy từ ComboBox
            {
                employeeUserName = (cbEmployees.SelectedValue != null && cbEmployees.SelectedValue.ToString() != "ALL_USERS")
                                   ? cbEmployees.SelectedValue.ToString()
                                   : null;
            }

            // Gọi hàm tải dữ liệu với các tham số đã thu thập.
            await LoadRevenueDataAsync(startDate, endDate, employeeUserName);
        }

        // Hàm chính để truy vấn CSDL và tải dữ liệu doanh thu.
        private async Task LoadRevenueDataAsync(DateTime? startDate, DateTime? endDate, string? userName)
        {
            var parameters = new List<SqlParameter>();
            // Bắt đầu xây dựng câu lệnh SQL.
            var queryBuilder = new StringBuilder(@"
                SELECT 
                    a.UserName,
                    a.DisplayName,
                    a.Type,
                    COUNT(b.ID) AS InvoiceCount,
                    ISNULL(SUM(b.TotalAmount), 0) AS TotalRevenue
                FROM Account a ");

            var joinConditions = new List<string> { "a.UserName = b.AccountUserName", "b.Status = 1" };

            // Thêm điều kiện lọc theo ngày nếu có.
            if (startDate.HasValue)
            {
                joinConditions.Add("b.DateCheckOut >= @StartDate");
                parameters.Add(new SqlParameter("@StartDate", startDate.Value));
            }
            if (endDate.HasValue)
            {
                joinConditions.Add("b.DateCheckOut <= @EndDate");
                parameters.Add(new SqlParameter("@EndDate", endDate.Value));
            }

            // Nối các điều kiện vào câu lệnh LEFT JOIN.
            queryBuilder.Append($" LEFT JOIN Bill b ON {string.Join(" AND ", joinConditions)} WHERE a.Type IN (0, 1) ");

            // Thêm điều kiện lọc theo nhân viên nếu có.
            if (!string.IsNullOrEmpty(userName))
            {
                queryBuilder.Append(" AND a.UserName = @UserName");
                parameters.Add(new SqlParameter("@UserName", userName));
            }

            // Hoàn thành câu lệnh với GROUP BY và ORDER BY.
            queryBuilder.Append(@"
                GROUP BY a.UserName, a.DisplayName, a.Type
                ORDER BY TotalRevenue DESC");

            using (var connection = new SqlConnection(connectionString))
            {
                var adapter = new SqlDataAdapter(queryBuilder.ToString(), connection);
                if (parameters.Any())
                {
                    adapter.SelectCommand.Parameters.AddRange(parameters.ToArray());
                }

                revenueDataTable = new DataTable();
                // Thêm các cột tạm thời để hiển thị trên giao diện.
                revenueDataTable.Columns.Add("STT", typeof(int));
                revenueDataTable.Columns.Add("Role", typeof(string));

                await Task.Run(() => adapter.Fill(revenueDataTable));

                // Xử lý dữ liệu sau khi tải: thêm số thứ tự và chuyển đổi loại tài khoản thành chuỗi.
                for (int i = 0; i < revenueDataTable.Rows.Count; i++)
                {
                    var row = revenueDataTable.Rows[i];
                    row["STT"] = i + 1;
                    row["Role"] = Convert.ToInt32(row["Type"]) == 1 ? "Admin" : "Nhân viên";
                }

                dgEmployeeRevenue.ItemsSource = revenueDataTable.DefaultView; // Gán dữ liệu cho DataGrid.
                CalculateTotals(); // Tính toán và hiển thị dòng tổng kết.
            }
        }

        // Tính tổng số hóa đơn và tổng doanh thu từ bảng dữ liệu đã tải.
        private void CalculateTotals()
        {
            long totalInvoices = 0;
            decimal totalRevenue = 0;

            foreach (DataRow row in revenueDataTable.Rows)
            {
                if (row["InvoiceCount"] != DBNull.Value)
                {
                    totalInvoices += Convert.ToInt64(row["InvoiceCount"]);
                }
                if (row["TotalRevenue"] != DBNull.Value)
                {
                    totalRevenue += Convert.ToDecimal(row["TotalRevenue"]);
                }
            }

            // Hiển thị kết quả lên các TextBlock.
            tbTotalInvoices.Text = $"{totalInvoices:N0}";
            tbTotalRevenue.Text = $"{totalRevenue:N0} VNĐ";
        }

        // Áp dụng các quy tắc phân quyền cho giao diện.
        private void ApplyAuthorization()
        {
            // Nếu người dùng là nhân viên (Type = 0).
            if (loggedInAccount?.Type == 0)
            {
                // Vô hiệu hóa ComboBox để không cho phép họ xem doanh thu của người khác.
                cbEmployees.IsEnabled = false;
            }
        }
    }
}