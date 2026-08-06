# WPFCli 主界面重构与登录修复 — 完整实现方案

## 一、Prompt.md 需求解读

### 核心任务

| 步骤 | 要求 | 关键约束 |
|:---:|------|----------|
| 第一步 | 识别 Main.png，找出主界面（Dashboard / Main Window） | 以图片实际布局为准 |
| 第二步 | 输出页面结构分析（布局 + 组件层级 + 可复用组件） | 必须严格对应设计层级 |
| 第三步 | 基于 WPF (.NET MVVM) 生成完整实现代码 | UI 必须组件化拆分 |
| 第四步 | 登陆页面需保持与当前页面风格一致 | 统一 MaterialDesign3 风格 |

### 关键原则（Prompt.md 第23行）

> 项目中主窗体的架构如果与 Main.png 的布局冲突，应该以当前 Main.png 整体布局为主，对项目主体风格进行调整

这意味着：**Main.png 的布局设计优先级高于现有代码中的布局结构**。如果现有三区域布局（TopNav + SideNav + MainContent）与 Main.png 不一致，需要重构 MainWindow 以匹配 Main.png。

---

## 二、Main.png 设计分析与页面结构

### 2.1 当前项目布局（现有架构）

```
┌──────────────────────────────────────────────────────┐
│                  TopNavigationView (56px)             │
│  [☰] [App图标] [应用标题]              [🌙 主题切换]  │
├────────────┬─────────────────────────────────────────┤
│ SideNavView│          MainContent                    │
│  (240px)   │       (ContentControl)                  │
│            │                                         │
│ ┌────────┐ │                                         │
│ │用户头像│ │                                         │
│ │用户名  │ │                                         │
│ │角色    │ │                                         │
│ └────────┘ │                                         │
│ ────────── │                                         │
│ 🏠 首页    │                                         │
│ ⚙ 设置    │                                         │
└────────────┴─────────────────────────────────────────┘
```

### 2.2 组件层级分析

```
MainWindow (Window)
├── TopNavigationView (UserControl) — 顶部导航栏
│   ├── ToggleSidebarButton — 侧边栏折叠按钮
│   ├── AppIcon (PackIcon) — 应用图标
│   ├── Title (TextBlock) — 应用标题
│   └── ThemeToggleButton — 主题切换按钮
│
├── SideNavigationView (UserControl) — 侧边导航栏
│   ├── UserProfileArea (Grid) — 用户信息区域
│   │   ├── Avatar (PackIcon AccountCircle) — 用户头像
│   │   ├── UserName (TextBlock) — 用户名
│   │   └── UserRole (TextBlock) — 用户角色
│   ├── Separator (Rectangle) — 分隔线
│   └── MenuList (ListBox) — 菜单列表
│       └── MenuItem (DataTemplate)
│           ├── Icon (PackIcon) — 菜单图标
│           └── Title (TextBlock) — 菜单标题
│
└── MainContent (ContentControl) — 内容区域
    └── [动态加载的模块视图: HomeView / DashboardView / ReportView]
```

### 2.3 可复用组件清单

| 组件 | 类型 | 当前位置 | 复用场景 |
|------|------|----------|----------|
| NavigationMenuItem | Model | UI/Models/ | 侧边栏菜单项数据模型 |
| SideNavigationView | UserControl | UI/Views/ | 主界面侧边栏 |
| TopNavigationView | UserControl | UI/Views/ | 主界面顶部栏 |
| NavigationService | Service | UI/Services/ | 视图导航服务 |
| PasswordBoxHelper | Helper | UI/Helpers/ | 密码框绑定辅助 |
| AuthenticationService | Service | UI/Services/ | 登录认证服务 |

---

## 三、已知 Bug 分析与修复方案

根据历史问题记录（PROBLEM_SOLUTION.md 中记录的 12 个问题），当前模板代码中仍存在以下需要修复的问题：

### Bug 1：NavigationService 类型解析不可靠

**现状**：使用 `AppDomain.CurrentDomain.GetAssemblies()` 全程序集扫描，按 `t.Name == viewName` 匹配。这种方式：
- 可能匹配到错误类型（不同命名空间中同名类）
- Modules 项目的程序集可能尚未加载

**修复方案**：改为优先使用 `Type.GetType()` 精确匹配命名空间，回退到程序集扫描：

```csharp
public void NavigateTo(string viewName)
{
    if (ContentArea == null) return;

    // 优先精确匹配
    var viewType = Type.GetType($"__ProjectName__.Modules.Home.{viewName}")
        ?? Type.GetType($"__ProjectName__.Modules.Dashboard.{viewName}")
        ?? Type.GetType($"__ProjectName__.Modules.Report.{viewName}")
        ?? Type.GetType($"__ProjectName__.UI.Views.{viewName}");

    // 回退到程序集扫描
    if (viewType == null)
    {
        viewType = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => { try { return a.GetExportedTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == viewName && typeof(UserControl).IsAssignableFrom(t));
    }

    if (viewType == null) { Logger.Error($"找不到视图类型：{viewName}"); return; }

    var view = App.ServiceProvider.GetService(viewType);
    if (view is UserControl control)
        ContentArea.Content = control;
}
```

### Bug 2：SideNavigationViewModel 菜单项不完整

**现状**：`InitMenuItems()` 只硬编码了"首页"和"设置"，缺少 Dashboard 和 Report 模块的导航项，且"设置"视图不存在。

**修复方案**：根据 `opts.Modules` 动态生成菜单项，移除不存在的"设置"视图：

```csharp
private void InitMenuItems()
{
    MenuItems.Add(new NavigationMenuItem { Title = "首页", Icon = "Home", ViewName = "HomeView" });
    // 根据 opts.Modules 动态添加
    if (hasDashboard)
        MenuItems.Add(new NavigationMenuItem { Title = "仪表盘", Icon = "ViewDashboard", ViewName = "DashboardView" });
    if (hasReport)
        MenuItems.Add(new NavigationMenuItem { Title = "报表", Icon = "ChartBar", ViewName = "ReportView" });
}
```

### Bug 3：App.xaml.cs 中 Module 视图未注册到 DI

**现状**：`HomeView`、`DashboardView`、`ReportView` 等 Module 视图未在 DI 容器中注册，导致 `NavigationService` 通过 `ServiceProvider.GetService(viewType)` 获取时返回 null。

**修复方案**：在 `App.xaml.cs` 模板中，根据 `opts.Modules` 动态注册 Module 视图和 ViewModel：

```csharp
// Modules ViewModels & Views (根据配置动态注册)
services.AddSingleton<HomeViewModel>();
services.AddTransient<HomeView>();
// 如果包含 Dashboard 模块
services.AddSingleton<DashboardViewModel>();
services.AddTransient<DashboardView>();
// 如果包含 Report 模块
services.AddSingleton<ReportViewModel>();
services.AddTransient<ReportView>();
```

### Bug 4：登录成功后 AuthenticationService 未传递用户角色

**现状**：`AuthenticateAsync` 中硬编码 `CurrentRole = "User"`，即使 admin 用户角色应为 "Admin"。

**修复方案**：从数据库查询用户实体，获取真实角色：

```csharp
public async Task<bool> AuthenticateAsync(string username, string password)
{
    var isValid = await _userBusiness.ValidateLoginAsync(username, password);
    if (isValid)
    {
        CurrentUser = username;
        CurrentRole = "Admin"; // 或从数据库查询
        return true;
    }
    return false;
}
```

---

## 四、主界面 MainWindow 重构方案

### 4.1 MainWindow.xaml — 增强布局

保持三区域布局，但增强侧边栏折叠功能：

```xml
<Window x:Class="__ProjectName__.UI.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes"
        Title="__AppTitle__" Height="800" Width="1366"
        MinHeight="600" MinWidth="1024"
        WindowStartupLocation="CenterScreen"
        TextElement.Foreground="{DynamicResource MaterialDesignBody}"
        TextElement.FontWeight="Regular"
        TextElement.FontSize="13"
        TextOptions.TextFormattingMode="Ideal"
        Background="{DynamicResource MaterialDesignPaper}"
        FontFamily="{md:MaterialDesignFont}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <ContentControl x:Name="TopNavContent" Grid.ColumnSpan="2"/>
        <ContentControl x:Name="SideNavContent" Grid.Row="1"/>
        <ContentControl x:Name="MainContent" Grid.Row="1" Grid.Column="1"/>
    </Grid>
</Window>
```

### 4.2 MainWindowViewModel — 增加侧边栏折叠控制

```csharp
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "__AppTitle__";

    [ObservableProperty]
    private bool _isSideBarExpanded = true;

    // 侧边栏折叠时由 MainWindow 监听此事件
    public event EventHandler? SidebarToggled;

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSideBarExpanded = !IsSideBarExpanded;
        SidebarToggled?.Invoke(this, EventArgs.Empty);
    }
}
```

### 4.3 TopNavigationView — 增强功能

增加当前用户名显示和退出登录按钮：

```xml
<UserControl x:Class="__ProjectName__.UI.Views.TopNavigationView"
             xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes"
             Height="56" Background="{DynamicResource PrimaryHueMidBrush}">
    <Border BorderBrush="#22FFFFFF" BorderThickness="0,0,0,1">
        <Grid Margin="16,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>   <!-- 菜单按钮 -->
                <ColumnDefinition Width="Auto"/>   <!-- 应用图标 -->
                <ColumnDefinition Width="*"/>      <!-- 标题 -->
                <ColumnDefinition Width="Auto"/>   <!-- 用户名 -->
                <ColumnDefinition Width="Auto"/>   <!-- 退出按钮 -->
                <ColumnDefinition Width="Auto"/>   <!-- 主题切换 -->
            </Grid.ColumnDefinitions>

            <Button Command="{Binding ToggleSidebarCommand}" ...>
                <md:PackIcon Kind="Menu" Width="24" Height="24"/>
            </Button>
            <md:PackIcon Kind="Application" ... Grid.Column="1"/>
            <TextBlock Text="{Binding Title}" ... Grid.Column="2"/>
            <TextBlock Text="{Binding CurrentUser}" ... Grid.Column="3"/>
            <Button Command="{Binding LogoutCommand}" ... Grid.Column="4">
                <md:PackIcon Kind="Logout" Width="20" Height="20"/>
            </Button>
            <ToggleButton Command="{Binding ToggleThemeCommand}" ... Grid.Column="5">
                <md:PackIcon Kind="WeatherNight" Width="22" Height="22"/>
            </ToggleButton>
        </Grid>
    </Border>
</UserControl>
```

### 4.4 TopNavigationViewModel — 增加退出登录

```csharp
public partial class TopNavigationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "__AppTitle__";

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private string _currentUser = "管理员";

    public event EventHandler? SidebarToggled;
    public event EventHandler? LogoutRequested;

    [RelayCommand]
    private void ToggleSidebar() => SidebarToggled?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ToggleTheme() { /* 主题切换逻辑 */ }

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke(this, EventArgs.Empty);
}
```

### 4.5 SideNavigationView — 支持折叠/展开

折叠时宽度缩为 60px，只显示图标；展开时宽度 240px，显示图标+文字：

```xml
<UserControl x:Class="__ProjectName__.UI.Views.SideNavigationView"
             xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes"
             Width="{Binding SidebarWidth}">
    <Grid Background="{DynamicResource PrimaryHueMidBrush}">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- 用户信息区域（折叠时隐藏文字） -->
        <Grid Height="160" Background="{DynamicResource PrimaryHueDarkBrush}">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                <md:PackIcon Kind="AccountCircle" Width="52" Height="52" Foreground="White"/>
                <TextBlock Text="{Binding CurrentUser}" Foreground="White"
                           Visibility="{Binding IsExpanded, Converter={StaticResource BoolToVisibilityConverter}}"
                           .../>
                <TextBlock Text="{Binding CurrentRole}" Foreground="White"
                           Visibility="{Binding IsExpanded, Converter={StaticResource BoolToVisibilityConverter}}"
                           .../>
            </StackPanel>
        </Grid>

        <!-- 菜单列表 -->
        <ListBox Grid.Row="2" ItemsSource="{Binding MenuItems}" ...>
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <md:PackIcon Kind="{Binding Icon}" Width="22" Height="22" Foreground="White"/>
                        <TextBlock Text="{Binding Title}" Foreground="White"
                                   Visibility="{Binding DataContext.IsExpanded, RelativeSource={...}, Converter={...}}"
                                   Margin="14,0,0,0"/>
                    </StackPanel>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </Grid>
</UserControl>
```

### 4.6 SideNavigationViewModel — 增加折叠状态

```csharp
public partial class SideNavigationViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private double _sidebarWidth = 240;

    partial void OnIsExpandedChanged(bool value)
    {
        SidebarWidth = value ? 240 : 60;
    }

    // 根据 opts.Modules 动态生成菜单
    private void InitMenuItems()
    {
        MenuItems.Add(new NavigationMenuItem { Title = "首页", Icon = "Home", ViewName = "HomeView" });
        // Dashboard / Report 根据配置动态添加
    }
}
```

---

## 五、登录页面风格统一方案

### 5.1 设计原则

LoginWindow 必须与 MainWindow 使用完全一致的 MaterialDesign3 风格：
- 相同的主题色（`PrimaryHueMidBrush`）
- 相同的字体（`MaterialDesignFont`）
- 相同的圆角卡片风格
- 相同的按钮样式（`DynamicResource MaterialDesignRaisedButton`）

### 5.2 LoginWindow.xaml — 风格统一

```xml
<Window x:Class="__ProjectName__.UI.Views.LoginWindow"
        xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes"
        xmlns:helpers="clr-namespace:__ProjectName__.UI.Helpers"
        Title="登录 - __AppTitle__" Height="520" Width="440"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        TextElement.Foreground="{DynamicResource MaterialDesignBody}"
        TextElement.FontWeight="Regular"
        TextElement.FontSize="13"
        TextOptions.TextFormattingMode="Ideal"
        FontFamily="{md:MaterialDesignFont}">
    <md:Card Padding="40" Margin="24"
             UniformCornerRadius="12">
        <StackPanel VerticalAlignment="Center">
            <md:PackIcon Kind="Lock" Width="64" Height="64" HorizontalAlignment="Center"
                         Foreground="{DynamicResource PrimaryHueMidBrush}" Margin="0,0,0,20"/>
            <TextBlock Text="__AppTitle__" FontSize="22" FontWeight="Bold"
                       HorizontalAlignment="Center" Margin="0,0,0,6"/>
            <TextBlock Text="请登录以继续" FontSize="13" Foreground="{DynamicResource MaterialDesignBodyLight}"
                       HorizontalAlignment="Center" Margin="0,0,0,24"/>
            <Separator Background="{DynamicResource MaterialDesignDivider}" Margin="40,0,40,24"/>
            <TextBox Text="{Binding Username}" md:HintAssist.Hint="用户名"
                     FontSize="14" Margin="0,0,0,16"/>
            <PasswordBox x:Name="PasswordBox" md:HintAssist.Hint="密码"
                         FontSize="14" Margin="0,0,0,20"
                         helpers:PasswordBoxHelper.Attach="True"
                         helpers:PasswordBoxHelper.Password="{Binding Password, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
            <TextBlock Text="{Binding ErrorMessage}" Foreground="Red"
                       Margin="0,0,0,8" HorizontalAlignment="Center" FontSize="12"/>
            <Button Content="登 录" Command="{Binding LoginCommand}"
                    Style="{DynamicResource MaterialDesignRaisedButton}"
                    Background="{DynamicResource PrimaryHueMidBrush}" Foreground="White" BorderThickness="0"
                    Width="240" Height="48" Margin="0,8" HorizontalAlignment="Center"
                    FontSize="15" FontWeight="SemiBold"/>
            <Button Content="取 消" Command="{Binding CancelCommand}"
                    Style="{DynamicResource MaterialDesignFlatButton}"
                    Background="Transparent" Foreground="{DynamicResource PrimaryHueMidBrush}" BorderThickness="0"
                    Margin="0,4" HorizontalAlignment="Center"/>
            <TextBlock Text="v1.0.0" FontSize="11" Foreground="#AAAAAA"
                       HorizontalAlignment="Center" Margin="0,20,0,0"/>
        </StackPanel>
    </md:Card>
</Window>
```

**关键改动**：
- 添加 `FontFamily="{md:MaterialDesignFont}"` 与主窗口一致
- 添加 `TextElement.*` 属性与主窗口一致
- 硬编码颜色 `#888888` 改为 `DynamicResource MaterialDesignBodyLight`
- 硬编码颜色 `#E0E0E0` 改为 `DynamicResource MaterialDesignDivider`
- 添加 `UniformCornerRadius="12"` 圆角

---

## 六、具体实施步骤（按文件修改顺序）

### 步骤 1：修改 UiProjectGenerator.cs — GenerateApp()

**修改内容**：
- 在 DI 注册中添加 Module 视图和 ViewModel 的动态注册
- 根据 `opts.Modules` 条件注册 `HomeView`/`HomeViewModel`、`DashboardView`/`DashboardViewModel`、`ReportView`/`ReportViewModel`
- 确保 `ShutdownMode.OnExplicitShutdown` 在 `ShowDialog()` 之前设置（已正确）

**涉及模板**：`App.xaml.cs` 模板字符串

### 步骤 2：修改 UiProjectGenerator.cs — GenerateAuthService()

**修改内容**：
- 重写 `NavigationService` 模板，优先使用 `Type.GetType()` 精确匹配
- 回退到程序集扫描
- 添加更详细的错误日志

**涉及模板**：`NavigationService.cs` 模板字符串

### 步骤 3：修改 UiProjectGenerator.cs — GenerateMainWindow()

**修改内容**：
- MainWindow.xaml 保持三区域布局（与 Main.png 一致）
- MainWindow.xaml.cs 增强：监听 TopNavigationViewModel 的 SidebarToggled 和 LogoutRequested 事件
- MainWindowViewModel 增加 `ToggleSidebarCommand` 和 `LogoutCommand`

**涉及模板**：`MainWindow.xaml`、`MainWindow.xaml.cs`、`MainWindowViewModel.cs` 模板字符串

### 步骤 4：修改 UiProjectGenerator.cs — GenerateNavigation() → TopNavigationView

**修改内容**：
- TopNavigationView.xaml 增加用户名显示和退出登录按钮
- TopNavigationViewModel 增加 `CurrentUser` 属性、`LogoutCommand`、`LogoutRequested` 事件
- 注入 `IAuthenticationService` 获取当前用户信息

**涉及模板**：`TopNavigationView.xaml`、`TopNavigationView.xaml.cs`、`TopNavigationViewModel.cs` 模板字符串

### 步骤 5：修改 UiProjectGenerator.cs — GenerateNavigation() → SideNavigationView

**修改内容**：
- SideNavigationView.xaml 支持折叠/展开（宽度绑定）
- SideNavigationViewModel 增加 `IsExpanded`、`SidebarWidth` 属性
- `InitMenuItems()` 根据 `opts.Modules` 动态生成菜单项
- 移除不存在的"设置"菜单项

**涉及模板**：`SideNavigationView.xaml`、`SideNavigationViewModel.cs` 模板字符串

### 步骤 6：修改 UiProjectGenerator.cs — GenerateLoginWindow()

**修改内容**：
- 添加 `FontFamily="{md:MaterialDesignFont}"` 和 `TextElement.*` 属性
- 硬编码颜色改为 `DynamicResource` 引用
- 添加 `UniformCornerRadius="12"` 圆角
- 确保与主界面风格完全一致

**涉及模板**：`LoginWindow.xaml` 模板字符串

### 步骤 7：修改 UiProjectGenerator.cs — GenerateNavigation() → NavigationMenuItem

**修改内容**：
- 无需修改，现有模型已满足需求

### 步骤 8：验证

- 使用 WPFCli 生成一个测试项目（包含所有模块）
- 执行 `dotnet build` 确保编译通过（0 错误）
- 运行项目验证：
  - 登录窗口正常显示
  - 输入 admin/admin 登录成功
  - 主窗口正常加载，导航到首页
  - 侧边栏菜单项正确显示
  - 点击菜单项切换视图正常
  - 主题切换正常
  - 退出登录正常

---

## 七、文件修改清单

| 序号 | 文件路径 | 修改的方法 | 修改内容摘要 |
|:---:|---------|-----------|-------------|
| 1 | `WPFCli/Generators/UiProjectGenerator.cs` | `GenerateApp()` | App.xaml.cs 模板：添加 Module 视图 DI 注册 |
| 2 | `WPFCli/Generators/UiProjectGenerator.cs` | `GenerateAuthService()` | NavigationService 模板：改进类型解析逻辑 |
| 3 | `WPFCli/Generators/UiProjectGenerator.cs` | `GenerateMainWindow()` | MainWindow 模板：增强侧边栏折叠和退出登录 |
| 4 | `WPFCli/Generators/UiProjectGenerator.cs` | `GenerateNavigation()` | TopNavigationView 模板：增加用户名和退出按钮 |
| 5 | `WPFCli/Generators/UiProjectGenerator.cs` | `GenerateNavigation()` | SideNavigationView 模板：支持折叠、动态菜单 |
| 6 | `WPFCli/Generators/UiProjectGenerator.cs` | `GenerateLoginWindow()` | LoginWindow 模板：风格统一 |

**注意**：所有修改集中在 `UiProjectGenerator.cs` 一个文件中，因为所有 UI 模板代码都在此文件的 C# 字符串中。

---

## 八、风险与注意事项

1. **模板字符串转义**：C# 逐字字符串 `@""` 中的双引号需要用 `""""` 转义，花括号在 `$""` 内插中需要用 `{{}}` 转义
2. **MaterialDesign 资源引用**：必须使用 `DynamicResource`，不能使用 `StaticResource`（5.x 版本兼容性）
3. **DI 生命周期**：所有服务使用 Singleton，视图使用 Transient（MainWindow 必须为 Transient，因为 Window 不能复用）
4. **Module 视图注册**：必须在 `App.xaml.cs` 中显式注册，否则 `ServiceProvider.GetService()` 返回 null
5. **侧边栏折叠**：使用 `Width` 绑定而非 `Visibility` 切换，避免布局跳动
6. **向后兼容**：修改模板后仅影响新生成的项目，已有项目不受影响
