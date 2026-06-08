using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace ActivationCodeTool;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void GenerateButton_OnClick(object sender, RoutedEventArgs e)
    {
        var equipmentName = EquipmentNameTextBox.Text.Trim();
        var equipmentModel = EquipmentModelTextBox.Text.Trim();
        var biosId = BiosIdTextBox.Text.Trim();
        var cpuId = CpuIdTextBox.Text.Trim();
        var diskId = DiskIdTextBox.Text.Trim();
        var machineCode = MachineCodeTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(equipmentName)
            || string.IsNullOrWhiteSpace(equipmentModel)
            || string.IsNullOrWhiteSpace(biosId)
            || string.IsNullOrWhiteSpace(cpuId)
            || string.IsNullOrWhiteSpace(diskId)
            || string.IsNullOrWhiteSpace(machineCode))
        {
            MessageBox.Show("请完整填写软件名称、产品型号、BIOS编号、CPU编号、硬盘信息和机器码。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LicenseKeyTextBox.Text = GenerateLicenseKey(equipmentName, equipmentModel, biosId, cpuId, diskId, machineCode);
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var licenseKey = LicenseKeyTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            MessageBox.Show("请先生成激活码。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(licenseKey);
        MessageBox.Show("激活码已复制。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        BiosIdTextBox.Clear();
        CpuIdTextBox.Clear();
        DiskIdTextBox.Clear();
        MachineCodeTextBox.Clear();
        LicenseKeyTextBox.Clear();
    }

    private void PasteDeviceInfoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsText())
        {
            MessageBox.Show("剪贴板中没有可读取的设备信息。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var values = ParseDeviceInfo(Clipboard.GetText());
        if (values.Count == 0)
        {
            MessageBox.Show("未识别到设备信息。请先在主程序激活页点击“复制设备信息”。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (values.TryGetValue("软件名称", out var equipmentName))
        {
            EquipmentNameTextBox.Text = equipmentName;
        }

        if (values.TryGetValue("产品型号", out var equipmentModel))
        {
            EquipmentModelTextBox.Text = equipmentModel;
        }

        if (values.TryGetValue("BIOS编号", out var biosId))
        {
            BiosIdTextBox.Text = biosId;
        }

        if (values.TryGetValue("CPU编号", out var cpuId))
        {
            CpuIdTextBox.Text = cpuId;
        }

        if (values.TryGetValue("硬盘信息", out var diskId))
        {
            DiskIdTextBox.Text = diskId;
        }

        if (values.TryGetValue("机器码", out var machineCode))
        {
            MachineCodeTextBox.Text = machineCode;
        }
    }

    private static string GenerateLicenseKey(
        string equipmentName,
        string equipmentModel,
        string biosId,
        string cpuId,
        string diskId,
        string machineCode)
    {
        var data = $"biosId={biosId}&cpuId={cpuId}&equipmentModel={equipmentModel}&equipmentName={equipmentName}&hdId={diskId}&uniqCode={machineCode}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(data));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return SplitEvery(hex, 4);
    }

    private static Dictionary<string, string> ParseDeviceInfo(string text)
    {
        var values = new Dictionary<string, string>();
        var separators = new[] { '：', ':', '=' };

        foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var index = line.IndexOfAny(separators);
            if (index <= 0 || index >= line.Length - 1)
            {
                continue;
            }

            var key = line[..index].Trim();
            var value = line[(index + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static string SplitEvery(string value, int groupLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        for (var i = 0; i < value.Length; i += groupLength)
        {
            parts.Add(value.Substring(i, Math.Min(groupLength, value.Length - i)));
        }

        return string.Join("-", parts);
    }
}
