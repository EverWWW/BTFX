from __future__ import annotations

import subprocess
import sys
from datetime import datetime
from pathlib import Path


# ========================= 参数配置 =========================
FFMPEG_EXE = r"C:\ffmpeg\bin\ffmpeg.exe"   # ffmpeg.exe 的完整路径
SAVE_DIR = r"D:\ZUT_test\video"            # 视频保存目录
DURATION_SEC = 6                           # 录制时长（秒）
VIDEO_SIZE = "3840x2160"                   # 采集分辨率
FPS = 59                                   # 采集帧率

CAMERA_NAMES = [
    "Y-CAM-25310455",
    "Y-CAM-25310080",
]                                           # 两台相机设备名

# 需要顺时针旋转 90 度的相机
ROTATE_90_CLOCKWISE_CAMERAS = {
    "Y-CAM-25310080",
}

DELETE_AVI_AFTER_MP4 = True                # 是否在转码成功后删除中间 AVI 文件
# ==========================================================


def safe_camera_tag(camera_name: str) -> str:
    """
    将相机名称转换为适合文件名的标识。
    """
    return camera_name.replace(" ", "_").replace("-", "_")


def print_log_tail(result: subprocess.CompletedProcess, stage_name: str) -> None:
    """
    打印 FFmpeg 返回码和日志末尾。
    """
    print(f"\n[{stage_name}] 返回码: {result.returncode}")
    print(f"\n[{stage_name}] 日志末尾：")
    print(result.stderr[-3000:] if result.stderr else "无日志输出")


def build_record_cmd(camera_name: str, avi_file: Path) -> list[str]:
    """
    构造单台相机直录 AVI 的 FFmpeg 命令。
    录制阶段不做旋转，确保采集链路最稳定。
    """
    return [
        FFMPEG_EXE,
        "-y",                              # 若文件已存在则覆盖
        "-f", "dshow",                     # Windows 下使用 DirectShow 采集相机
        "-rtbufsize", "1024M",             # 增大实时缓冲区，降低高码率采集丢帧风险
        "-video_size", VIDEO_SIZE,         # 指定采集分辨率
        "-framerate", str(FPS),            # 指定采集帧率
        "-vcodec", "mjpeg",                # 指定相机输出格式为 MJPEG
        "-i", f"video={camera_name}",      # 指定视频采集设备
        "-t", str(DURATION_SEC),           # 指定录制时长
        "-c", "copy",                      # 直接拷贝 MJPEG 码流，不重新编码
        str(avi_file),                     # AVI 输出文件路径
    ]


def build_transcode_cmd(
    avi_file: Path,
    mp4_file: Path,
    rotate_clockwise_90: bool = False,
) -> list[str]:
    """
    构造 AVI 转 MP4（CPU / libx264）的 FFmpeg 命令。

    rotate_clockwise_90=True 时：
    对视频做顺时针 90 度旋转。
    FFmpeg 中 transpose=1 表示顺时针 90 度。
    """
    if rotate_clockwise_90:
        vf_expr = "transpose=1,scale=in_range=pc:out_range=tv,format=yuv420p"
    else:
        vf_expr = "scale=in_range=pc:out_range=tv,format=yuv420p"

    return [
        FFMPEG_EXE,
        "-y",                              # 若文件已存在则覆盖
        "-i", str(avi_file),               # 输入 AVI 文件
        "-vf", vf_expr,                    # 旋转（可选）+ 像素范围与格式转换
        "-c:v", "libx264",                 # 使用 CPU 的 libx264 编码 H.264
        "-preset", "veryfast",             # 编码速度较快，适合工程使用
        "-crf", "18",                      # 画质控制参数
        "-movflags", "+faststart",         # 优化 MP4 文件结构，便于快速打开
        str(mp4_file),                     # MP4 输出文件路径
    ]


def start_record_process(camera_name: str, avi_file: Path) -> subprocess.Popen:
    """
    启动单台相机的 AVI 录制进程（异步）。
    """
    cmd = build_record_cmd(camera_name, avi_file)

    print(f"\n========== 启动录制：{camera_name} ==========")
    print("执行命令：")
    print(" ".join(cmd))
    print("========================================")

    process = subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="ignore",
    )
    return process


def run_transcode(
    avi_file: Path,
    mp4_file: Path,
    camera_name: str,
    rotate_clockwise_90: bool = False,
) -> None:
    """
    执行单台相机的 AVI -> MP4 转码。
    """
    cmd = build_transcode_cmd(
        avi_file=avi_file,
        mp4_file=mp4_file,
        rotate_clockwise_90=rotate_clockwise_90,
    )

    stage_name = f"步骤2：{camera_name} AVI 转 MP4"
    print(f"\n========== {stage_name} ==========")
    print("执行命令：")
    print(" ".join(cmd))
    print("================================")

    result = subprocess.run(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="ignore",
    )

    print_log_tail(result, stage_name)

    if result.returncode != 0:
        raise RuntimeError(f"{camera_name} 的 MP4 转码失败。")

    if not mp4_file.exists():
        raise RuntimeError(f"{camera_name} 的 MP4 文件不存在。")


def main() -> None:
    # 创建保存目录
    save_dir = Path(SAVE_DIR)
    save_dir.mkdir(parents=True, exist_ok=True)

    # 生成统一时间戳，保证两台相机属于同一批次
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")

    # 为每台相机构造输出文件路径
    tasks: list[dict] = []
    for camera_name in CAMERA_NAMES:
        tag = safe_camera_tag(camera_name)
        avi_file = save_dir / f"{timestamp}_{tag}.avi"
        mp4_file = save_dir / f"{timestamp}_{tag}.mp4"

        tasks.append(
            {
                "camera_name": camera_name,
                "tag": tag,
                "avi_file": avi_file,
                "mp4_file": mp4_file,
                "rotate_clockwise_90": camera_name in ROTATE_90_CLOCKWISE_CAMERAS,
            }
        )

    try:
        print("开始执行双相机两步式视频处理流程。")
        print(f"录制规格: {VIDEO_SIZE} @ {FPS}fps")
        print(f"录制时长: {DURATION_SEC} 秒")
        print("相机列表：")
        for task in tasks:
            rotate_text = "是" if task["rotate_clockwise_90"] else "否"
            print(f"  - {task['camera_name']}")
            print(f"    AVI: {task['avi_file']}")
            print(f"    MP4: {task['mp4_file']}")
            print(f"    顺时针旋转90度: {rotate_text}")

        # =========================
        # 步骤1：两台相机同时直录 AVI
        # =========================
        processes: list[dict] = []

        for task in tasks:
            process = start_record_process(task["camera_name"], task["avi_file"])
            processes.append(
                {
                    "camera_name": task["camera_name"],
                    "avi_file": task["avi_file"],
                    "process": process,
                }
            )

        print("\n[信息] 两台相机已启动录制，等待录制完成...")

        # 等待两台相机录制完成
        for item in processes:
            camera_name = item["camera_name"]
            avi_file = item["avi_file"]
            process = item["process"]

            stdout_data, stderr_data = process.communicate()

            fake_result = subprocess.CompletedProcess(
                args=process.args,
                returncode=process.returncode,
                stdout=stdout_data,
                stderr=stderr_data,
            )

            stage_name = f"步骤1：{camera_name} 直录 AVI"
            print_log_tail(fake_result, stage_name)

            if process.returncode != 0:
                print(f"\n[错误] {camera_name} 的 AVI 录制失败，流程终止。")
                sys.exit(1)

            if not avi_file.exists():
                print(f"\n[错误] {camera_name} 的 AVI 文件不存在，流程终止。")
                sys.exit(1)

        print("\n[信息] 两台相机 AVI 录制均成功，开始顺序转码为 MP4。")

        # =========================
        # 步骤2：顺序将两台相机的 AVI 转 MP4
        # =========================
        for task in tasks:
            run_transcode(
                avi_file=task["avi_file"],
                mp4_file=task["mp4_file"],
                camera_name=task["camera_name"],
                rotate_clockwise_90=task["rotate_clockwise_90"],
            )

            print(f"\n[信息] {task['camera_name']} 的 MP4 转码成功。")

            # 根据设置决定是否删除中间 AVI 文件
            if DELETE_AVI_AFTER_MP4:
                try:
                    task["avi_file"].unlink()
                    print(f"[信息] 已删除中间 AVI 文件: {task['avi_file']}")
                except Exception as exc:
                    print(f"[警告] AVI 删除失败: {exc}")

        print("\n========== 全流程完成 ==========")
        for task in tasks:
            print(f"{task['camera_name']} 最终 MP4 文件: {task['mp4_file']}")
            if not DELETE_AVI_AFTER_MP4:
                print(f"{task['camera_name']} 中间 AVI 文件: {task['avi_file']}")

    except FileNotFoundError:
        print("[错误] 找不到 ffmpeg.exe，请检查 FFMPEG_EXE 路径。")
        sys.exit(1)
    except Exception as exc:
        print(f"[错误] 程序运行失败: {exc}")
        sys.exit(1)


if __name__ == "__main__":
    main()