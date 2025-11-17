﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace namm
{
    // Lớp xử lý logic cho màn hình Dashboard, nơi quản lý bàn, menu và hóa đơn.
    public partial class DashboardView : UserControl
    {
        // Chuỗi kết nối CSDL.
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        // Bảng dữ liệu cho menu và danh sách bàn.
        private DataTable? menuDataTable;
        private DataTable? tableDataTable;
        // Dùng Dictionary để lưu trữ hóa đơn (danh sách các món) cho mỗi bàn, với Key là ID của bàn.
        private readonly Dictionary<int, ObservableCollection<BillItem>> billsByTable = new Dictionary<int, ObservableCollection<BillItem>>();
        // Lưu thông tin tài khoản đang đăng nhập.
        private readonly AccountDTO? loggedInAccount;

        public DashboardView(AccountDTO? account = null)
        {
            InitializeComponent();
            this.loggedInAccount = account; // Nhận thông tin tài khoản từ cửa sổ chính.
        }

        // Sự kiện được gọi khi UserControl được tải xong.
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Tải dữ liệu song song để tăng tốc độ khởi động và giữ cho UI luôn phản hồi.
                var loadTablesTask = LoadTables();
                var loadCategoriesTask = LoadCategories();
                var loadMenuTask = LoadMenu();

                await Task.WhenAll(loadTablesTask, loadCategoriesTask, loadMenuTask); // Chờ tất cả các tác vụ tải dữ liệu hoàn thành.

                // Gắn sự kiện sau khi đã tải xong dữ liệu ban đầu
                cbCategory.SelectionChanged += CbCategory_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nghiêm trọng khi tải màn hình chính: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Tải danh sách các loại đồ uống (category) vào ComboBox.
        private async Task LoadCategories()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Thêm mục "Tất cả" vào danh sách
                const string query = "SELECT 0 AS ID, N'Tất cả' AS Name UNION ALL SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                var categoryTable = new DataTable();
                // Chạy tác vụ lấy dữ liệu trên một luồng nền để không làm treo giao diện.
                await Task.Run(() => adapter.Fill(categoryTable));

                // Gán dữ liệu cho ComboBox.
                cbCategory.ItemsSource = categoryTable.DefaultView;
                cbCategory.SelectedValuePath = "ID";
                cbCategory.DisplayMemberPath = "Name";
                cbCategory.SelectedIndex = 0; // Mặc định chọn "Tất cả"
            }
        }
        // Tải danh sách các đồ uống đang hoạt động vào DataGrid menu.
        private async Task LoadMenu()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Chỉ lấy những đồ uống đang được kích hoạt để hiển thị trên menu
                const string query = "SELECT ID, Name, DrinkCode, ActualPrice, CategoryID FROM Drink WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                menuDataTable = new DataTable();
                menuDataTable.Columns.Add("STT", typeof(int));
                await Task.Run(() => adapter.Fill(menuDataTable));

                // Gán dữ liệu cho DataGrid menu.
                dgMenu.ItemsSource = menuDataTable.DefaultView;
            }
        }
        // Tải danh sách các bàn ăn vào DataGrid bàn.
        private async Task LoadTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, Status FROM TableFood ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                tableDataTable = new DataTable();
                tableDataTable.Columns.Add("STT", typeof(int));
                await Task.Run(() => adapter.Fill(tableDataTable));

                // Gán dữ liệu cho DataGrid bàn.
                dgTables.ItemsSource = tableDataTable.DefaultView;
            }
        }

        // Sự kiện khi người dùng thay đổi lựa chọn trong ComboBox loại đồ uống.
        private void CbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterMenu();
        }

        private void FilterMenu()
        {
            if (menuDataTable == null || cbCategory.SelectedValue == null)
            {
                return;
            }

            int categoryId = (int)cbCategory.SelectedValue;

            if (categoryId == 0) // ID 0 là của mục "Tất cả".
            {
                menuDataTable.DefaultView.RowFilter = string.Empty; // Xóa bộ lọc
            }
            else
            {
                menuDataTable.DefaultView.RowFilter = $"CategoryID = {categoryId}"; // Lọc theo CategoryID
            }
        }

        // Sự kiện được gọi khi một dòng đang được tải, dùng để đánh số thứ tự.
        private void DgTables_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgMenu_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        // Sự kiện quan trọng: được gọi khi người dùng chọn một bàn khác.
        private async void DgTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nếu có một bàn được chọn.
            if (dgTables.SelectedItem is DataRowView selectedTable)
            {
                string tableName = selectedTable["Name"].ToString() ?? "Không xác định";
                tbSelectedTable.Text = $"Chọn: {tableName}"; // Cập nhật tiêu đề hóa đơn.
                tbSelectedTable.FontStyle = FontStyles.Normal;
                tbSelectedTable.Foreground = System.Windows.Media.Brushes.Black;

                // Lấy ID của bàn và tải hóa đơn chưa thanh toán (nếu có) từ CSDL.
                int tableId = (int)selectedTable["ID"];
                var billItems = await LoadUnpaidBillForTableAsync(tableId);
                billsByTable[tableId] = billItems; // Lưu hóa đơn vào bộ nhớ đệm (dictionary).

                // Hiển thị hóa đơn của bàn đã chọn lên DataGrid.
                dgBill.ItemsSource = billItems;
                UpdateTotalAmount(); // Cập nhật tổng tiền.

                // Đồng bộ lại trạng thái bàn (Trống/Có người) dựa trên hóa đơn.
                SyncTableStatusBasedOnBill();
            }
            else
            {
                // Nếu không có bàn nào được chọn, xóa thông tin hóa đơn.
                tbSelectedTable.Text = "(Chưa chọn bàn)";
                tbSelectedTable.FontStyle = FontStyles.Italic;
                tbSelectedTable.Foreground = System.Windows.Media.Brushes.Gray;

                // Khi không có bàn nào được chọn, xóa hiển thị hóa đơn
                dgBill.ItemsSource = null;
                UpdateTotalAmount();
            }
        }

        // Sự kiện quan trọng: được gọi khi người dùng double-click vào một món trong menu.
        private async void DgMenu_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Kiểm tra xem đã chọn bàn chưa.
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn trước khi thêm món.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgMenu.SelectedItem is DataRowView selectedDrinkRow)
            {
                try
                {
                    int drinkId = (int)selectedDrinkRow["ID"];
                    string drinkName = selectedDrinkRow["Name"].ToString() ?? "Không tên";

                    // Lấy dữ liệu tồn kho (nguyên bản và pha chế) để không làm treo UI.
                    var availableStock = await GetDrinkStockAsync(drinkId);

                    if (!availableStock.Any())
                    {
                        MessageBox.Show("Đồ uống này chưa được cấu hình để bán (chưa có giá hoặc công thức).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Mở cửa sổ để người dùng chọn loại (nguyên bản/pha chế) và nhập số lượng.
                    var dialog = new SelectDrinkTypeDialog(drinkName, availableStock);
                    dialog.Owner = Window.GetWindow(this);

                    if (dialog.ShowDialog() == true)
                    {
                        // Lấy hóa đơn của bàn hiện tại từ bộ nhớ đệm.
                        int currentTableId = (int)((DataRowView)dgTables.SelectedItem)["ID"];
                        var currentBillItems = billsByTable[currentTableId];

                        foreach (var selectedItem in dialog.SelectedQuantities)
                        {
                            string drinkType = selectedItem.Key;
                            int quantity = selectedItem.Value;
                            decimal price = Convert.ToDecimal(selectedDrinkRow["ActualPrice"]);
                            string baseDrinkCode = selectedDrinkRow["DrinkCode"].ToString() ?? "";

                            // Kiểm tra xem món này đã có trong hóa đơn chưa.
                            var existingItem = currentBillItems.FirstOrDefault(item => item.DrinkId == drinkId && item.DrinkType == drinkType);

                            if (existingItem != null)
                            {
                                existingItem.Quantity += quantity; // Nếu có rồi, chỉ tăng số lượng.
                            }
                            else
                            {
                                // Nếu chưa có, thêm một món mới vào hóa đơn.
                                currentBillItems.Add(new BillItem { 
                                    DrinkId = drinkId, 
                                    DrinkName = drinkName, 
                                    DrinkTypeCode = $"{baseDrinkCode}_{(drinkType == "Nguyên bản" ? "NB" : "PC")}",
                                    DrinkType = drinkType, 
                                    Quantity = quantity, 
                                    Price = price 
                                });
                            }
                            // Trừ số lượng tồn kho tương ứng.
                            await UpdateStockForDrinkAsync(drinkId, drinkType, quantity);
                        }
                        // Lưu toàn bộ hóa đơn vào CSDL.
                        await SaveBillToDbAsync(currentTableId, currentBillItems);

                        UpdateTotalAmount(); // Cập nhật lại tổng tiền.
                        SyncTableStatusBasedOnBill();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Đã xảy ra lỗi không mong muốn: {ex.Message}", "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Cập nhật hiển thị tổng tiền của hóa đơn.
        private void UpdateTotalAmount()
        {
            // Tính tổng tiền dựa trên danh sách các món đang hiển thị.
            if (dgBill.ItemsSource is ObservableCollection<BillItem> currentBillItems)
            {
                decimal total = currentBillItems.Sum(item => item.TotalPrice);
                tbTotalAmount.Text = $"{total:N0} VNĐ";
            }
            else
            {
                tbTotalAmount.Text = "0 VNĐ";
            }
        }

        // Xử lý sự kiện khi nhấn nút "Xóa" trên một món trong hóa đơn.
        private async void DeleteBillItem_Click(object sender, RoutedEventArgs e)
        {
            // Lấy món cần xóa từ CommandParameter của nút.
            if ((sender as Button)?.CommandParameter is BillItem itemToRemove && dgBill.ItemsSource is ObservableCollection<BillItem> currentBillItems)
            {
                // Hoàn trả lại số lượng vào kho (số lượng âm).
                _ = UpdateStockForDrinkAsync(itemToRemove.DrinkId, itemToRemove.DrinkType, -itemToRemove.Quantity);

                // Xóa món khỏi danh sách hiển thị.
                currentBillItems.Remove(itemToRemove);
                UpdateTotalAmount();

                // Nếu hóa đơn vẫn còn món, lưu lại vào CSDL.
                if (currentBillItems.Any())
                {
                    int currentTableId = (int)((DataRowView)dgTables.SelectedItem)["ID"];
                    await SaveBillToDbAsync(currentTableId, currentBillItems);
                }
                // Đồng bộ lại trạng thái bàn. Nếu hóa đơn trống, bàn sẽ chuyển về "Trống".
                SyncTableStatusBasedOnBill();
            }
        }

        // Lấy số lượng tồn kho của một đồ uống (cả dạng nguyên bản và pha chế).
        private async Task<Dictionary<string, int>> GetDrinkStockAsync(int drinkId)
        {
            var stock = new Dictionary<string, int>();
            using (var connection = new SqlConnection(connectionString))
            {
                // 1. Lấy số lượng tồn kho của đồ uống nguyên bản
                var cmdOriginal = new SqlCommand("SELECT StockQuantity FROM Drink WHERE ID = @ID AND OriginalPrice > 0", connection);
                cmdOriginal.Parameters.AddWithValue("@ID", drinkId);

                // 2. Tính số lượng có thể làm của đồ uống pha chế
                var cmdRecipe = new SqlCommand(@"
                    SELECT MIN(FLOOR(m.Quantity / r.Quantity))
                    FROM Recipe r
                    JOIN Material m ON r.MaterialID = m.ID
                    JOIN Drink d ON r.DrinkID = d.ID
                    WHERE r.DrinkID = @ID AND d.IsRecipeActive = 1
                    -- Chỉ tính khi có công thức tồn tại
                    HAVING COUNT(r.DrinkID) > 0", connection);
                cmdRecipe.Parameters.AddWithValue("@ID", drinkId);

                await connection.OpenAsync();

                var originalStockResult = await cmdOriginal.ExecuteScalarAsync();
                if (originalStockResult != null && originalStockResult != DBNull.Value)
                {
                    stock["Nguyên bản"] = Convert.ToInt32(originalStockResult);
                }

                var recipeStockResult = await cmdRecipe.ExecuteScalarAsync();
                if (recipeStockResult != null && recipeStockResult != DBNull.Value)
                {
                    stock["Pha chế"] = Convert.ToInt32(recipeStockResult);
                }
            }
            return stock;
        }

        // Cập nhật trạng thái của bàn trong CSDL.
        private async Task UpdateTableStatusInDbAsync(int tableId, string newStatus)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    var command = new SqlCommand("UPDATE TableFood SET Status = @Status WHERE ID = @ID", connection);
                    command.Parameters.AddWithValue("@Status", newStatus);
                    command.Parameters.AddWithValue("@ID", tableId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Ghi lại lỗi hoặc hiển thị một thông báo không làm phiền người dùng
                MessageBox.Show($"Không thể cập nhật trạng thái bàn vào cơ sở dữ liệu: {ex.Message}", "Lỗi nền", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Cập nhật số lượng tồn kho (trừ khi thêm món, cộng lại khi xóa món).
        private async Task UpdateStockForDrinkAsync(int drinkId, string drinkType, int quantityChange)
        {
            // quantityChange > 0: Trừ kho (thêm món)
            // quantityChange < 0: Hoàn kho (xóa món)
            if (quantityChange == 0) return;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                if (drinkType == "Nguyên bản")
                {
                    // Nếu là đồ uống nguyên bản, chỉ cần cập nhật bảng Drink.
                    var cmd = new SqlCommand("UPDATE Drink SET StockQuantity = StockQuantity - @QuantityChange WHERE ID = @DrinkID", connection);
                    cmd.Parameters.AddWithValue("@DrinkID", drinkId);
                    cmd.Parameters.AddWithValue("@QuantityChange", quantityChange);
                    await cmd.ExecuteNonQueryAsync();
                }
                else if (drinkType == "Pha chế")
                {
                    // Nếu là đồ uống pha chế, lấy công thức để trừ kho nguyên liệu.
                    var recipeCmd = new SqlCommand("SELECT MaterialID, Quantity FROM Recipe WHERE DrinkID = @DrinkID", connection);
                    recipeCmd.Parameters.AddWithValue("@DrinkID", drinkId);

                    var materialsToUpdate = new List<(int MaterialID, decimal RecipeQuantity)>();
                    using (var reader = await recipeCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            materialsToUpdate.Add((reader.GetInt32(0), reader.GetDecimal(1)));
                        }
                    }

                    // Cập nhật số lượng cho từng nguyên liệu trong công thức.
                    foreach (var material in materialsToUpdate)
                    {
                        var updateMaterialCmd = new SqlCommand("UPDATE Material SET Quantity = Quantity - @QuantityChange WHERE ID = @MaterialID", connection);
                        decimal totalMaterialChange = (decimal)material.RecipeQuantity * quantityChange;
                        updateMaterialCmd.Parameters.AddWithValue("@MaterialID", material.MaterialID);
                        updateMaterialCmd.Parameters.AddWithValue("@QuantityChange", totalMaterialChange);
                        await updateMaterialCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // Tải hóa đơn chưa thanh toán (Status = 0) của một bàn từ CSDL.
        private async Task<ObservableCollection<BillItem>> LoadUnpaidBillForTableAsync(int tableId)
        {
            var billItems = new ObservableCollection<BillItem>();
            using (var connection = new SqlConnection(connectionString))
            {
                // Tìm hóa đơn chưa thanh toán (Status = 0) của bàn
                const string query = @"                    SELECT bi.DrinkID, d.Name, bi.DrinkType, bi.Quantity, bi.Price, d.DrinkCode
                    FROM BillInfo bi
                    JOIN Bill b ON bi.BillID = b.ID
                    JOIN Drink d ON bi.DrinkID = d.ID
                    WHERE b.TableID = @TableID AND b.Status = 0";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableID", tableId);

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string drinkType = reader.GetString(2);
                        string baseDrinkCode = reader.GetString(5);

                        billItems.Add(new BillItem
                        {
                            // Xây dựng lại DrinkTypeCode khi tải từ DB
                            DrinkTypeCode = $"{baseDrinkCode}_{(drinkType == "Nguyên bản" ? "NB" : "PC")}",
                            DrinkId = reader.GetInt32(0),
                            DrinkName = reader.GetString(1),
                            DrinkType = reader.GetString(2),
                            Quantity = reader.GetInt32(3),
                            Price = reader.GetDecimal(4)
                        });
                    }
                }
            }
            return billItems;
        }

        // Lưu hoặc cập nhật một hóa đơn vào CSDL.
        private async Task SaveBillToDbAsync(int tableId, ObservableCollection<BillItem> billItems)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Tìm hóa đơn chưa thanh toán (Status=0) của bàn.
                        var cmdFindBill = new SqlCommand("SELECT ID FROM Bill WHERE TableID = @TableID AND Status = 0", connection, transaction);
                        cmdFindBill.Parameters.AddWithValue("@TableID", tableId);
                        var billIdResult = await cmdFindBill.ExecuteScalarAsync();
                        int billId;

                        if (billIdResult != null)
                        {
                            billId = (int)billIdResult;
                            // Nếu có, xóa các chi tiết hóa đơn cũ để ghi đè bằng thông tin mới.
                            var cmdDeleteInfo = new SqlCommand("DELETE FROM BillInfo WHERE BillID = @BillID", connection, transaction);
                            cmdDeleteInfo.Parameters.AddWithValue("@BillID", billId);
                            await cmdDeleteInfo.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            // Tạo hóa đơn mới
                            var cmdCreateBill = new SqlCommand("INSERT INTO Bill (TableID, Status, AccountUserName) OUTPUT INSERTED.ID VALUES (@TableID, 0, @AccountUserName)", connection, transaction);
                            cmdCreateBill.Parameters.AddWithValue("@TableID", tableId);
                            cmdCreateBill.Parameters.AddWithValue("@AccountUserName", loggedInAccount?.UserName ?? (object)DBNull.Value);
                            billId = (int)await cmdCreateBill.ExecuteScalarAsync();
                        }

                        // 2. Thêm lại các chi tiết hóa đơn từ danh sách hiện tại.
                        foreach (var item in billItems)
                        {
                            var cmdInsertInfo = new SqlCommand("INSERT INTO BillInfo (BillID, DrinkID, DrinkType, Quantity, Price) VALUES (@BillID, @DrinkID, @DrinkType, @Quantity, @Price)", connection, transaction);
                            cmdInsertInfo.Parameters.AddWithValue("@BillID", billId);
                            // Lưu ý: DrinkTypeCode không được lưu vào DB, nó chỉ dùng để hiển thị
                            cmdInsertInfo.Parameters.AddWithValue("@DrinkID", item.DrinkId);
                            cmdInsertInfo.Parameters.AddWithValue("@DrinkType", item.DrinkType);
                            cmdInsertInfo.Parameters.AddWithValue("@Quantity", item.Quantity);
                            cmdInsertInfo.Parameters.AddWithValue("@Price", item.Price);
                            await cmdInsertInfo.ExecuteNonQueryAsync();
                        }

                        // 3. Cập nhật tổng tiền cho hóa đơn
                        decimal totalAmount = billItems.Sum(i => i.TotalPrice);
                        var cmdUpdateTotal = new SqlCommand("UPDATE Bill SET TotalAmount = @TotalAmount WHERE ID = @BillID", connection, transaction);
                        cmdUpdateTotal.Parameters.AddWithValue("@TotalAmount", totalAmount);
                        cmdUpdateTotal.Parameters.AddWithValue("@BillID", billId);
                        await cmdUpdateTotal.ExecuteNonQueryAsync();

                        transaction.Commit(); // Hoàn tất giao dịch.
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw; // Ném lại lỗi để được xử lý ở lớp ngoài
                    }
                }
            }
        }

        // Xóa hóa đơn rỗng khỏi CSDL.
        private async Task ClearBillFromDbAsync(int tableId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // Để đảm bảo an toàn, chúng ta sẽ xóa các BillInfo trước, sau đó mới xóa Bill
                const string query = @"
                    DELETE FROM BillInfo WHERE BillID IN (SELECT ID FROM Bill WHERE TableID = @TableID AND Status = 0);
                    DELETE FROM Bill WHERE TableID = @TableID AND Status = 0;";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableID", tableId);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        // Đồng bộ trạng thái của bàn trên giao diện và trong CSDL dựa vào việc hóa đơn có món hay không.
        private void SyncTableStatusBasedOnBill()
        {
            if (dgTables.SelectedItem is DataRowView selectedTable && dgBill.ItemsSource is ObservableCollection<BillItem> currentBill)
            {
                int tableId = (int)selectedTable.Row["ID"];
                string currentStatus = selectedTable.Row["Status"].ToString();
                string newStatus;

                if (currentBill.Any())
                {
                    // Nếu hóa đơn có món, trạng thái phải là "Có người".
                    newStatus = "Có người";
                }
                else
                {
                    // Nếu hóa đơn trống, trạng thái phải là "Trống".
                    newStatus = "Trống";
                    // Đồng thời xóa hóa đơn rỗng khỏi CSDL.
                    _ = ClearBillFromDbAsync(tableId);
                }

                // Nếu trạng thái trên giao diện khác với trạng thái mới, cập nhật lại.
                if (currentStatus != newStatus)
                {
                    selectedTable.Row["Status"] = newStatus;
                    _ = UpdateTableStatusInDbAsync(tableId, newStatus); // Cập nhật vào CSDL.
                }
            }
        }

        // Xử lý sự kiện khi nhấn nút "Thanh toán".
        private void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            // 1. Kiểm tra đã chọn bàn và có hóa đơn chưa
            if (dgTables.SelectedItem == null || !(dgBill.ItemsSource is ObservableCollection<BillItem> currentBill) || !currentBill.Any())
            {
                MessageBox.Show("Vui lòng chọn bàn có hóa đơn để thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Lấy thông tin cần thiết để truyền đi
            var selectedTable = (DataRowView)dgTables.SelectedItem;
            int tableId = (int)selectedTable["ID"];
            string tableName = selectedTable["Name"].ToString();

            // 3. Chuyển sang màn hình chọn khách hàng (SelectCustomerView).
            var mainAppWindow = Window.GetWindow(this) as MainAppWindow;
            if (mainAppWindow != null)
            {
                // Xóa nội dung cũ và thêm view mới vào
                mainAppWindow.MainContent.Children.Clear();
                // Lấy thông tin tài khoản đang đăng nhập từ MainAppWindow và truyền vào
                mainAppWindow.MainContent.Children.Add(new SelectCustomerView(tableId, tableName, currentBill, mainAppWindow.LoggedInAccount));
            }
        }
    }
}