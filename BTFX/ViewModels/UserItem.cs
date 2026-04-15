using BTFX.Common;
using BTFX.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels;

/// <summary>
/// 用户列表项
/// </summary>
public partial class UserItem : ObservableObject
{
    public User User { get; }

    public int RowNumber { get; }

    public string DepartmentName { get; }

    public string Username => User.Username;

    public string Name => User.Name ?? "--";

    public string RoleDisplay => GetLocalizedRole(User.Role);

    public string Phone => User.Phone ?? "--";

    public string StatusDisplay => User.IsEnabled ? GetLocalizedString("Enabled") : GetLocalizedString("Disabled");

    public string StatusColor => User.IsEnabled ? "#4CAF50" : "#9E9E9E";

    public string CreatedAtDisplay => User.CreatedAt.ToString(BtfxConstants.DATETIME_LIST_FORMAT);

    public string LastLoginDisplay => User.LastLoginAt?.ToString(BtfxConstants.DATETIME_LIST_FORMAT) ?? GetLocalizedString("NeverLoggedIn");

    [ObservableProperty]
    private bool _isChecked;

    public bool IsBuiltIn => User.Username == BtfxConstants.ADMIN_USERNAME ||
                             User.Username == BtfxConstants.USER_USERNAME ||
                             User.Username == BtfxConstants.GUEST_USERNAME;

    public UserItem(User user, int rowNumber, string? departmentName = null)
    {
        User = user;
        RowNumber = rowNumber;
        DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? "--" : departmentName;
    }

    private static string GetLocalizedString(string key)
    {
        try
        {
            var resource = System.Windows.Application.Current.FindResource(key);
            return resource?.ToString() ?? key;
        }
        catch
        {
            return key;
        }
    }

    private static string GetLocalizedRole(UserRole role)
    {
        return role switch
        {
            UserRole.Administrator => GetLocalizedString("Administrator"),
            UserRole.Operator => GetLocalizedString("Operator"),
            UserRole.Guest => GetLocalizedString("Guest"),
            _ => "--"
        };
    }
}
