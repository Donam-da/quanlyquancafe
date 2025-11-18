﻿﻿// File này chứa logic xử lý cho cửa sổ chính của ứng dụng (MainAppWindow.xaml).
// Chức năng chính là điều hướng: khi người dùng click vào một mục trong menu,
// nó sẽ hiển thị màn hình (UserControl) tương ứng trong khu vực nội dung chính.
// Nó cũng chịu trách nhiệm phân quyền, ẩn các chức năng không phù hợp với vai trò người dùng.
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace namm
{
    // Lớp MainAppWindow đại diện cho cửa sổ chính của ứng dụng.
    public partial class MainAppWindow : Window
    {
        // Thuộc tính để lưu trữ thông tin của tài khoản đang đăng nhập.
        public AccountDTO LoggedInAccount { get; private set; }

        // Hàm khởi tạo (constructor), nhận vào một đối tượng AccountDTO chứa thông tin người dùng.
        public MainAppWindow(AccountDTO account)
        {
            InitializeComponent(); // Tải các thành phần giao diện từ file XAML.
            this.LoggedInAccount = account; // Lưu lại thông tin tài khoản.
            LoadApplicationTheme(); // Tải và áp dụng màu nền đã lưu.

            // Hiển thị màn hình Sơ đồ bàn (DashboardView) làm màn hình mặc định khi vừa đăng nhập.
            MainContent.Children.Add(new DashboardView(LoggedInAccount));
            Authorize(); // Gọi hàm phân quyền để ẩn/hiện các mục menu.
        }

        // Tải và áp dụng màu nền cho ứng dụng từ cài đặt đã lưu.
        private void LoadApplicationTheme()
        {
            try
            {
                // Đọc chuỗi màu từ file cài đặt (Properties.Settings).
                string? bgColor = Properties.Settings.Default.AppBackgroundColor;
                if (!string.IsNullOrEmpty(bgColor))
                {
                    var converter = new BrushConverter();
                    this.Background = (Brush?)converter.ConvertFromString(bgColor); // Chuyển chuỗi màu thành Brush và áp dụng cho nền của Window.
                }
            }
            catch (Exception)
            {
                // Nếu có lỗi (ví dụ: chuỗi màu không hợp lệ), bỏ qua và giữ nguyên màu mặc định.
            }
        }

        // Hàm phân quyền: ẩn các chức năng chỉ dành cho Admin nếu người dùng là nhân viên.
        void Authorize()
        {
            // Kiểm tra loại tài khoản. Nếu Type = 0 (Nhân viên), ẩn các mục menu quản lý và thống kê cấp cao.
            if (LoggedInAccount.Type == 0)
            {
                miManageEmployees.Visibility = Visibility.Collapsed; // Ẩn "Quản lí nhân viên".
                miInvoiceHistory.Visibility = Visibility.Collapsed; // Ẩn "Lịch sử hóa đơn".
                miDeleteHistory.Visibility = Visibility.Collapsed; // Ẩn "Xóa lịch sử hóa đơn".
                miProfitStatistics.Visibility = Visibility.Collapsed; // Ẩn "Thống kê lợi nhuận".
            }
        }

        // Xử lý sự kiện click cho các menu cấp cao nhất để bật/tắt menu con.
        private void TopLevelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsSubmenuOpen = !menuItem.IsSubmenuOpen;
            }
        }

        // Hiển thị màn hình Quản lý nhân viên.
        private void ManageEmployees_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear(); // Xóa nội dung cũ.
            MainContent.Children.Add(new EmployeeView()); // Thêm UserControl EmployeeView vào.
        }

        // Xử lý chức năng Đăng xuất.
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            MainWindow loginWindow = new MainWindow(); // Tạo một cửa sổ đăng nhập mới.
            loginWindow.Show(); // Hiển thị cửa sổ đăng nhập.
            this.Close(); // Đóng cửa sổ chính hiện tại.
        }

        // Hiển thị màn hình Đổi mật khẩu.
        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // Tạo view đổi mật khẩu, truyền thông tin tài khoản đang đăng nhập vào.
            var changePasswordView = new ChangePasswordView(LoggedInAccount);

            // Lắng nghe sự kiện LogoutRequested từ ChangePasswordView. Khi đổi mật khẩu thành công, view này sẽ yêu cầu đăng xuất.
            changePasswordView.LogoutRequested += (s, args) => Logout_Click(s!, null!);

            MainContent.Children.Clear();
            MainContent.Children.Add(changePasswordView);
        }

        // Hiển thị màn hình Quản lý bàn.
        private void ManageTables_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new TableView());
        }

        // Hiển thị màn hình Quản lý đơn vị tính.
        private void ManageUnits_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new UnitView());
        }

        // Hiển thị màn hình Quản lý nguyên liệu.
        private void ManageMaterials_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new MaterialView());
        }

        // Hiển thị màn hình Quản lý đồ uống nguyên bản.
        private void ManageOriginalDrinks_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new DrinkView());
        }

        // Hiển thị màn hình Quản lý menu đồ uống (tổng).
        private void ManageMenu_Click(object sender, RoutedEventArgs e)
        {
            // Chỉ thực hiện hành động khi người dùng click trực tiếp vào menu cha, không phải khi click vào một menu con.
            if (e.OriginalSource == sender)
            {
                MainContent.Children.Clear();
                MainContent.Children.Add(new MenuView());
            }
        }

        // Hiển thị màn hình Quản lý công thức (đồ uống pha chế).
        private void ManageRecipes_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new RecipeView());
        }

        // Hiển thị màn hình Trang chủ (Sơ đồ bàn).
        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new DashboardView(LoggedInAccount));
        }

        // Hiển thị màn hình Quản lý loại đồ uống.
        private void ManageCategories_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new CategoryView());
        }

        // Hiển thị màn hình Lịch sử hóa đơn.
        private void InvoiceHistory_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new InvoiceHistoryView());
        }

        // Hiển thị màn hình Thống kê khách hàng thân thiết.
        private void LoyalCustomers_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new LoyalCustomerView());
        }

        // Hiển thị màn hình Thống kê top hàng bán chạy.
        private void TopSelling_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new TopSellingItemsView());
        }

        // Hiển thị màn hình Thống kê lợi nhuận.
        private void miProfitStatistics_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new ProfitStatisticsView());
        }

        // Hiển thị màn hình Xóa lịch sử hóa đơn.
        private void DeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new DeleteHistoryView());
        }

        // Hiển thị màn hình Thống kê doanh thu nhân viên.
        private void EmployeeRevenue_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new EmployeeRevenueView(this.LoggedInAccount));
        }

        // Hiển thị màn hình Cài đặt giao diện.
        private void InterfaceSettings_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Children.Clear();
            MainContent.Children.Add(new InterfaceSettingsView());
        }
    }
}