using BTFX.Models.Analysis;
using OxyPlot;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 图表生成服务接口
/// 提供步态分析报告中各类图表的创建、导出和数据读取功能
/// </summary>
public interface IChartService
{
    /// <summary>
    /// 创建关节角度-时间曲线图
    /// </summary>
    /// <param name="data">关节角度帧数据</param>
    /// <param name="jointName">关节名称（hip / knee / ankle / pelvis）</param>
    /// <param name="title">图表标题</param>
    /// <returns>OxyPlot PlotModel</returns>
    PlotModel CreateJointAnglePlot(List<JointAngleFrame> data, string jointName, string title);

    /// <summary>
    /// 创建关键点速度曲线图
    /// </summary>
    /// <param name="data">速度帧数据</param>
    /// <param name="keypointName">关键点名称</param>
    /// <param name="title">图表标题</param>
    /// <returns>OxyPlot PlotModel</returns>
    PlotModel CreateVelocityPlot(List<KeypointVelocityFrame> data, string keypointName, string title);

    /// <summary>
    /// 创建关节角速度曲线图
    /// </summary>
    /// <param name="data">角速度帧数据</param>
    /// <param name="jointName">关节名称</param>
    /// <param name="title">图表标题</param>
    /// <returns>OxyPlot PlotModel</returns>
    PlotModel CreateAngularVelocityPlot(List<JointAngularVelocityFrame> data, string jointName, string title);

    /// <summary>
    /// 创建关键点运动轨迹图
    /// </summary>
    /// <param name="data">轨迹帧数据</param>
    /// <param name="keypointName">关键点名称</param>
    /// <param name="title">图表标题</param>
    /// <returns>OxyPlot PlotModel</returns>
    PlotModel CreateTrajectoryPlot(List<KeypointTrajectoryFrame> data, string keypointName, string title);

    /// <summary>
    /// 将 PlotModel 导出为 PNG 字节数组（用于 PDF 嵌入）
    /// </summary>
    /// <param name="model">图表模型</param>
    /// <param name="width">图片宽度（像素）</param>
    /// <param name="height">图片高度（像素）</param>
    /// <returns>PNG 字节数组</returns>
    byte[] ExportPlotToPng(PlotModel model, int width = 480, int height = 240);

    /// <summary>
    /// 读取关节角度 CSV 文件
    /// </summary>
    /// <param name="csvPath">CSV 文件路径</param>
    /// <returns>关节角度帧列表</returns>
    List<JointAngleFrame> ReadJointAngleCsv(string csvPath);

    /// <summary>
    /// 读取关键点轨迹 CSV 文件
    /// </summary>
    /// <param name="csvPath">CSV 文件路径</param>
    /// <returns>关键点轨迹帧列表</returns>
    List<KeypointTrajectoryFrame> ReadKeypointTrajectoryCsv(string csvPath);

    /// <summary>
    /// 读取关键点速度 CSV 文件
    /// </summary>
    /// <param name="csvPath">CSV 文件路径</param>
    /// <returns>关键点速度帧列表</returns>
    List<KeypointVelocityFrame> ReadKeypointVelocityCsv(string csvPath);

    /// <summary>
    /// 读取关节角速度 CSV 文件
    /// </summary>
    /// <param name="csvPath">CSV 文件路径</param>
    /// <returns>关节角速度帧列表</returns>
    List<JointAngularVelocityFrame> ReadJointAngularVelocityCsv(string csvPath);
}
