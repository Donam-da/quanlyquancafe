﻿// Đây là file code-behind cho ProfitStatisticsView.xaml, chứa toàn bộ logic xử lý cho màn hình thống kê lợi nhuận.
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace namm
{
    // Lớp ProfitStatisticsView đại diện cho UserControl thống kê lợi nhuận.
    public partial class ProfitStatisticsView : UserControl
    {
        // Chuỗi kết nối đến cơ sở dữ liệu, được lấy từ file App.config.
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // DataTable để lưu trữ dữ liệu lợi nhuận đã được xử lý và tính toán, dùng làm nguồn cho DataGrid.
        private DataTable profitDataTable = new DataTable();

        // Hàm khởi tạo của UserControl.
        public ProfitStatisticsView()
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
        }

        // Sự kiện được gọi khi UserControl đã được tải xong.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Tạm thời gỡ bỏ các event handler để tránh việc chúng bị kích hoạt và gọi FilterData() nhiều lần khi ta thiết lập giá trị ban đầu cho các control.
            dpStartDate.SelectedDateChanged -= DpStartDate_SelectedDateChanged;
            dpStartDate.SelectedDateChanged -= Filters_Changed;
            dpEndDate.SelectedDateChanged -= Filters_Changed;
            cbFilterDrinkName.SelectionChanged -= Filters_Changed;
            cbFilterDrinkType.SelectionChanged -= Filters_Changed;
            cbFilterCategory.SelectionChanged -= Filters_Changed;

            // Tải dữ liệu cho các ComboBox bộ lọc (Tên đồ uống, Loại, Kiểu).
            await LoadFilterComboBoxes();

            // Lấy ngày có hóa đơn thanh toán sớm nhất từ cơ sở dữ liệu.
            DateTime? firstInvoiceDate = await GetFirstInvoiceDateAsync();

            // Thiết lập giá trị mặc định cho các DatePicker.
            // Nếu có hóa đơn, ngày bắt đầu là ngày của hóa đơn đầu tiên. Nếu không, là ngày hôm nay.
            dpStartDate.SelectedDate = firstInvoiceDate?.Date ?? DateTime.Today;
            // Ngày kết thúc mặc định là ngày hôm nay.
            dpEndDate.SelectedDate = DateTime.Today;
            
            // Gắn lại các event handler sau khi đã thiết lập xong giá trị ban đầu.
            dpStartDate.SelectedDateChanged += DpStartDate_SelectedDateChanged;
            dpStartDate.SelectedDateChanged += Filters_Changed;
            dpEndDate.SelectedDateChanged += Filters_Changed;
            cbFilterDrinkName.SelectionChanged += Filters_Changed;
            cbFilterDrinkType.SelectionChanged += Filters_Changed;
            cbFilterCategory.SelectionChanged += Filters_Changed;

            // Gọi FilterData() lần đầu tiên để tải dữ liệu ban đầu dựa trên các giá trị mặc định.
            await FilterData();
        }

        // Hàm bất đồng bộ để lấy ngày có hóa đơn đầu tiên từ CSDL.
        private async Task<DateTime?> GetFirstInvoiceDateAsync()
        {
            // Sử dụng 'using' để đảm bảo kết nối được đóng lại ngay cả khi có lỗi.
            using (var connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL để lấy ngày thanh toán (DateCheckOut) nhỏ nhất từ bảng Bill, chỉ xét các hóa đơn đã hoàn thành (Status = 1).
                const string query = "SELECT MIN(DateCheckOut) FROM Bill WHERE Status = 1";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync(); // Mở kết nối đến CSDL.
                var result = await command.ExecuteScalarAsync(); // Thực thi câu lệnh và lấy về giá trị duy nhất.
                // Kiểm tra nếu kết quả trả về không phải null hoặc DBNull.
                if (result != null && result != DBNull.Value)
                {
                    return (DateTime)result; // Ép kiểu kết quả sang DateTime và trả về.
                }
            }
            return null; // Trả về null nếu không tìm thấy hóa đơn nào.
        }

        // Hàm bất đồng bộ để tải dữ liệu cho các ComboBox dùng để lọc.
        private async Task LoadFilterComboBoxes()
        {
            // Tải dữ liệu cho ComboBox lọc theo Tên đồ uống.
            using (var connection = new SqlConnection(connectionString))
            {
                // Câu lệnh SQL sử dụng UNION ALL để gộp dòng 'Tất cả' vào danh sách đồ uống.
                // Cột SortOrder đảm bảo 'Tất cả' luôn được xếp ở đầu danh sách.
                const string query = @"
                    SELECT 0 AS ID, N'Tất cả' AS Name, 0 AS SortOrder
                    UNION ALL
                    SELECT DISTINCT ID, Name, 1 AS SortOrder FROM Drink ORDER BY SortOrder, Name";
                var adapter = new SqlDataAdapter(query, connection); // Tạo một SqlDataAdapter để lấy dữ liệu.
                var drinkTable = new DataTable(); // Tạo một DataTable để chứa kết quả.
                await Task.Run(() => adapter.Fill(drinkTable)); // Chạy việc điền dữ liệu vào DataTable trên một luồng khác để không block UI.
                cbFilterDrinkName.ItemsSource = drinkTable.DefaultView; // Gán nguồn dữ liệu cho ComboBox.
                cbFilterDrinkName.SelectedIndex = 0; // Chọn 'Tất cả' làm giá trị mặc định.
            }

            // Tải dữ liệu cho ComboBox lọc theo Kiểu đồ uống (cố định).
            cbFilterDrinkType.Items.Add("Tất cả"); // Thêm mục 'Tất cả'.
            cbFilterDrinkType.Items.Add("Pha chế"); // Thêm mục 'Pha chế'.
            cbFilterDrinkType.Items.Add("Nguyên bản"); // Thêm mục 'Nguyên bản'.
            cbFilterDrinkType.SelectedIndex = 0; // Chọn 'Tất cả' làm giá trị mặc định.

            // Tải dữ liệu cho ComboBox lọc theo Loại đồ uống.
            using (var connection = new SqlConnection(connectionString))
            {
                // Tương tự như trên, dùng UNION ALL và SortOrder để thêm mục 'Tất cả' lên đầu.
                const string query = @"
                    SELECT 0 AS ID, N'Tất cả' AS Name, 0 AS SortOrder 
                    UNION ALL 
                    SELECT ID, Name, 1 AS SortOrder FROM Category WHERE IsActive = 1 ORDER BY SortOrder, Name";
                var adapter = new SqlDataAdapter(query, connection);
                var categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable));
                cbFilterCategory.ItemsSource = categoryTable.DefaultView; // Gán nguồn dữ liệu.
                cbFilterCategory.SelectedIndex = 0; // Chọn 'Tất cả' làm giá trị mặc định.
            }
        }

        // Sự kiện click của nút "Lọc".
        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            await FilterData(); // Gọi hàm lọc dữ liệu.
        }

        // Sự kiện được chia sẻ bởi các control lọc (DatePicker, ComboBox) khi giá trị của chúng thay đổi.
        private async void Filters_Changed(object sender, RoutedEventArgs e)
        {
            // Kiểm tra IsLoaded để đảm bảo UserControl đã được tải hoàn toàn.
            // Điều này ngăn hàm FilterData() bị gọi nhiều lần trong quá trình khởi tạo giao diện.
            if (!this.IsLoaded) return;
            await FilterData(); // Gọi hàm lọc dữ liệu.
        }

        // Hàm chính để lấy các giá trị từ bộ lọc và tải dữ liệu tương ứng.
        private async Task FilterData()
        {
            // Lấy giá trị ngày bắt đầu từ DatePicker.
            DateTime? startDate = dpStartDate.SelectedDate?.Date;
            // Lấy giá trị ngày kết thúc và cộng thêm 1 ngày rồi trừ đi 1 tick để bao gồm toàn bộ ngày được chọn (đến 23:59:59.999).
            DateTime? endDate = dpEndDate.SelectedDate?.Date.AddDays(1).AddTicks(-1);
            // Lấy ID đồ uống được chọn. Nếu là 'Tất cả' (ID=0), giá trị sẽ là null.
            int? drinkIdFilter = (cbFilterDrinkName.SelectedValue != null && (int)cbFilterDrinkName.SelectedValue > 0) ? (int)cbFilterDrinkName.SelectedValue : (int?)null;
            // Lấy kiểu đồ uống được chọn. Nếu là 'Tất cả' (index=0), giá trị sẽ là null.
            string? drinkTypeFilter = cbFilterDrinkType.SelectedIndex > 0 ? cbFilterDrinkType.SelectedItem.ToString() : null;
            // Lấy ID loại đồ uống được chọn. Nếu là 'Tất cả' (ID=0), giá trị sẽ là null.
            int? categoryFilter = (cbFilterCategory.SelectedValue != null && (int)cbFilterCategory.SelectedValue > 0) ? (int)cbFilterCategory.SelectedValue : (int?)null;

            // Gọi hàm tải dữ liệu lợi nhuận với các tham số lọc đã lấy.
            await LoadProfitDataAsync(startDate, endDate, drinkIdFilter, drinkTypeFilter, categoryFilter);
        }

        // Hàm bất đồng bộ để tải và xử lý dữ liệu lợi nhuận từ CSDL.
        private async Task LoadProfitDataAsync(DateTime? startDate, DateTime? endDate, int? drinkId, string? drinkType, int? categoryId)
        {
            var parameters = new List<SqlParameter>(); // Danh sách để chứa các tham số cho câu lệnh SQL.

            using (var connection = new SqlConnection(connectionString))
            {
                // Sử dụng StringBuilder để xây dựng câu lệnh SQL một cách linh hoạt.
                var queryBuilder = new System.Text.StringBuilder(@"
                    -- Sử dụng Common Table Expression (CTE) để tính toán trước một số thông tin cần thiết.
                    WITH BillItemDetails AS (
                        SELECT
                            bi.BillID, bi.DrinkID, bi.DrinkType, bi.Quantity, bi.Price,
                            (bi.Quantity * bi.Price) AS ItemRevenue, -- Doanh thu của từng món trước khi giảm giá.
                            b.SubTotal, b.TotalAmount -- Tổng tiền hàng và tổng tiền thanh toán của hóa đơn.
                        FROM BillInfo bi
                        JOIN Bill b ON bi.BillID = b.ID WHERE b.Status = 1 -- Chỉ lấy các hóa đơn đã thanh toán.
                    )
                    SELECT 
                        d.Name AS DrinkName, 
                        d.CategoryID,
                        bid.DrinkType, -- Kiểu đồ uống (Pha chế/Nguyên bản).
                        SUM(bid.Quantity) AS TotalQuantitySold, -- Tổng số lượng bán.
                    -- Giá vốn đơn vị: Dùng ISNULL để đảm bảo giá trị không bao giờ là NULL, mặc định là 0.
                    ISNULL(CASE 
                        WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost 
                        WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice 
                        ELSE 0 
                    END, 0) AS UnitCost,
                    -- Tổng vốn: Tính tổng của (số lượng * giá vốn đơn vị).
                    SUM(bid.Quantity * ISNULL(CASE 
                        WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost 
                        WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice 
                        ELSE 0 
                    END, 0)) AS TotalCost,
                    -- Tổng doanh thu: Tính toán doanh thu thực tế của từng món sau khi đã áp dụng giảm giá của toàn hóa đơn.
                    -- Công thức: Doanh thu món = (Doanh thu món / Tổng tiền hàng) * Tổng tiền thanh toán.
                    -- Cách này phân bổ giảm giá của hóa đơn cho từng món một cách tỉ lệ, đảm bảo tổng doanh thu các món bằng tổng doanh thu các hóa đơn.
                    SUM(
                        CASE 
                            -- Kiểm tra SubTotal > 0 để tránh lỗi chia cho 0.
                            -- Ép kiểu sang DECIMAL để đảm bảo phép chia là số thực, tránh sai số làm tròn.
                            WHEN bid.SubTotal > 0 THEN 
                                (CAST(bid.ItemRevenue AS DECIMAL(18, 2)) * CAST(bid.TotalAmount AS DECIMAL(18, 2))) / CAST(bid.SubTotal AS DECIMAL(18, 2))
                            ELSE bid.ItemRevenue -- Nếu không có giảm giá, doanh thu món bằng giá trị ban đầu.
                        END
                    ) AS TotalRevenue
                    FROM BillItemDetails bid
                    JOIN Drink d ON bid.DrinkID = d.ID
                    WHERE 1=1 "); // Mệnh đề WHERE luôn đúng để dễ dàng nối thêm các điều kiện lọc.

                // Thêm điều kiện lọc theo ngày bắt đầu nếu có.
                if (startDate.HasValue)
                {
                    queryBuilder.Append(" AND bid.BillID IN (SELECT ID FROM Bill WHERE DateCheckOut >= @StartDate)");
                    parameters.Add(new SqlParameter("@StartDate", startDate.Value));
                }

                // Thêm điều kiện lọc theo ngày kết thúc nếu có.
                if (endDate.HasValue)
                {
                    queryBuilder.Append(" AND bid.BillID IN (SELECT ID FROM Bill WHERE DateCheckOut <= @EndDate)");
                    parameters.Add(new SqlParameter("@EndDate", endDate.Value));
                }

                // Thêm điều kiện lọc theo ID đồ uống nếu có.
                if (drinkId.HasValue)
                {
                    queryBuilder.Append(" AND d.ID = @DrinkID");
                    parameters.Add(new SqlParameter("@DrinkID", drinkId.Value));
                }
                // Thêm điều kiện lọc theo kiểu đồ uống nếu có.
                if (!string.IsNullOrWhiteSpace(drinkType))
                {
                    queryBuilder.Append(" AND bid.DrinkType = @DrinkType");
                    parameters.Add(new SqlParameter("@DrinkType", drinkType));
                }
                // Thêm điều kiện lọc theo ID loại đồ uống nếu có.
                if (categoryId.HasValue)
                {
                    queryBuilder.Append(" AND d.CategoryID = @CategoryID");
                    parameters.Add(new SqlParameter("@CategoryID", categoryId.Value));
                }

                // Thêm mệnh đề GROUP BY và ORDER BY.
                queryBuilder.Append(@"
                    GROUP BY d.Name, d.CategoryID, bid.DrinkType, d.RecipeCost, d.OriginalPrice
                    ORDER BY Profit DESC;
                ");

                // Vì cột 'Profit' không tồn tại trực tiếp trong câu lệnh SELECT, ta phải thay thế 'ORDER BY Profit'
                // bằng biểu thức tính lợi nhuận (Doanh thu - Vốn) để sắp xếp.
                string finalQuery = queryBuilder.ToString().Replace("ORDER BY Profit DESC", 
                    @"ORDER BY 
                        (SUM(
                            CASE 
                                WHEN bid.SubTotal > 0 THEN 
                                    (CAST(bid.ItemRevenue AS DECIMAL(18, 2)) * CAST(bid.TotalAmount AS DECIMAL(18, 2))) / CAST(bid.SubTotal AS DECIMAL(18, 2))
                                ELSE bid.ItemRevenue 
                            END
                        )) 
                        - 
                        (SUM(bid.Quantity * ISNULL(CASE WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice ELSE 0 END, 0))) 
                    DESC");


                var adapter = new SqlDataAdapter(finalQuery, connection); // Tạo adapter với câu lệnh SQL cuối cùng.
                adapter.SelectCommand.Parameters.AddRange(parameters.ToArray()); // Thêm các tham số đã thu thập vào câu lệnh.

                profitDataTable = new DataTable(); // Khởi tạo lại DataTable.
                // Thêm các cột sẽ được tính toán sau khi lấy dữ liệu từ CSDL.
                profitDataTable.Columns.Add("STT", typeof(int)); // Cột số thứ tự.
                profitDataTable.Columns.Add("Profit", typeof(decimal)); // Cột lợi nhuận.
                profitDataTable.Columns.Add("ProfitMargin", typeof(decimal)); // Cột tỷ suất lợi nhuận.

                // Điền dữ liệu từ CSDL vào DataTable.
                await Task.Run(() => adapter.Fill(profitDataTable));

                // Duyệt qua từng dòng trong DataTable vừa lấy về để tính toán các giá trị còn thiếu.
                for (int i = 0; i < profitDataTable.Rows.Count; i++)
                {
                    var row = profitDataTable.Rows[i];
                    row["STT"] = i + 1; // Gán số thứ tự.

                    decimal totalRevenue = Convert.ToDecimal(row["TotalRevenue"]); // Lấy tổng doanh thu.
                    decimal totalCost = Convert.ToDecimal(row["TotalCost"]); // Lấy tổng vốn.
                    decimal profit = totalRevenue - totalCost; // Tính lợi nhuận.
                    row["Profit"] = profit; // Gán giá trị lợi nhuận vào cột 'Profit'.

                    // Tính tỷ suất lợi nhuận (Profit Margin), kiểm tra totalRevenue > 0 để tránh lỗi chia cho 0.
                    row["ProfitMargin"] = (totalRevenue > 0) ? (profit / totalRevenue) * 100 : 0;
                }

                dgProfitStats.ItemsSource = profitDataTable.DefaultView; // Gán nguồn dữ liệu cho DataGrid.
                CalculateTotals(); // Tính toán và hiển thị các con số tổng hợp (tổng doanh thu, tổng vốn, tổng lợi nhuận).
            }
        }
        // Hàm tính toán và hiển thị các giá trị tổng cộng.
        private void CalculateTotals()
        {
            decimal totalRevenue = 0;
            decimal totalCost = 0;

            // Duyệt qua DataTable đã được lọc để cộng dồn doanh thu và chi phí.
            foreach (DataRow row in profitDataTable.Rows)
            {
                if (row["TotalRevenue"] != DBNull.Value) // Kiểm tra giá trị không phải là null.
                {
                    totalRevenue += Convert.ToDecimal(row["TotalRevenue"]);
                }
                if (row["TotalCost"] != DBNull.Value) // Kiểm tra giá trị không phải là null.
                {
                    totalCost += Convert.ToDecimal(row["TotalCost"]);
                }
            }
            // Hiển thị các giá trị tổng đã tính toán lên các TextBlock tương ứng, định dạng số có dấu phẩy.
            tbTotalRevenue.Text = $"{totalRevenue:N0} VNĐ";
            tbTotalCost.Text = $"{totalCost:N0} VNĐ";
            tbTotalProfit.Text = $"{totalRevenue - totalCost:N0} VNĐ";
        }

        // Sự kiện khi ngày bắt đầu được thay đổi.
        private void DpStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Logic này đảm bảo người dùng không thể chọn ngày kết thúc (dpEndDate) trước ngày bắt đầu (dpStartDate).
            if (dpStartDate.SelectedDate.HasValue)
            {
                // Đặt ngày bắt đầu có thể chọn cho DatePicker 'Đến ngày' là ngày đã chọn ở 'Từ ngày'.
                dpEndDate.DisplayDateStart = dpStartDate.SelectedDate;

                // Nếu ngày kết thúc hiện tại đang nhỏ hơn ngày bắt đầu mới được chọn, tự động cập nhật ngày kết thúc bằng ngày bắt đầu.
                if (dpEndDate.SelectedDate < dpStartDate.SelectedDate)
                {
                    dpEndDate.SelectedDate = dpStartDate.SelectedDate;
                }
            }
        }
    }
}