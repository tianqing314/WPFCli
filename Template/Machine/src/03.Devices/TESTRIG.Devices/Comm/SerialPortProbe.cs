using System.IO.Ports;

namespace TESTRIG.Devices.Comm;

/// <summary>
/// 串口占用预检：真正 <see cref="SerialPort.Open"/> 前先探测端口能否打开，把原生
/// <c>UnauthorizedAccessException</c>（"Access to the path 'COMx' is denied"）翻译成操作员可读的中文提示
/// （被占用 / 不存在 / 无权限），避免驱动层抛出晦涩英文原生异常。探测后立即关闭，不占端口。
/// </summary>
public static class SerialPortProbe
{
    /// <summary>
    /// 探测某 COM 口是否可打开。
    /// </summary>
    /// <param name="com">COM 名（如 "COM8"）。</param>
    /// <returns>(是否可用, 说明)。</returns>
    public static (bool Ok, string Message) Probe(string com)
    {
        if (string.IsNullOrWhiteSpace(com))
        {
            return (false, "未指定串口");
        }

        try
        {
            using var sp = new SerialPort(com);
            sp.Open();
            return (true, $"{com} 可用");
        }
        catch (UnauthorizedAccessException)
        {
            // 端口被其它程序/另一个本程序实例/串口助手占用，或当前账户无权限
            return (false, $"{com} 被占用或无访问权限（请关闭占用该串口的程序/多余的本程序实例后重试）");
        }
        catch (FileNotFoundException)
        {
            return (false, $"{com} 不存在（设备未插入或驱动未安装）");
        }
        catch (Exception ex)
        {
            return (false, $"{com} 打开失败：{ex.Message}");
        }
    }
}
