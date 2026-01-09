def binary_search(arr, target):
    """
    二分查找算法
    
    参数:
        arr: 已排序的列表
        target: 要查找的目标值
    
    返回:
        如果找到，返回目标值的索引；否则返回 -1
    """
    left, right = 0, len(arr) - 1
    
    while left <= right:
        mid = (left + right) // 2
        
        if arr[mid] == target:
            return mid
        elif arr[mid] < target:
            left = mid + 1
        else:
            right = mid - 1
    
    return -1


def binary_search_recursive(arr, target, left=0, right=None):
    """
    递归实现的二分查找算法
    
    参数:
        arr: 已排序的列表
        target: 要查找的目标值
        left: 左边界
        right: 右边界
    
    返回:
        如果找到，返回目标值的索引；否则返回 -1
    """
    if right is None:
        right = len(arr) - 1
    
    if left > right:
        return -1
    
    mid = (left + right) // 2
    
    if arr[mid] == target:
        return mid
    elif arr[mid] < target:
        return binary_search_recursive(arr, target, mid + 1, right)
    else:
        return binary_search_recursive(arr, target, left, mid - 1)


# 示例程序
if __name__ == "__main__":
    # 测试数据（必须是已排序的数组）
    sorted_array = [1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25]
    
    print("=" * 50)
    print("二分查找算法演示")
    print("=" * 50)
    print(f"数组: {sorted_array}")
    print()
    
    # 测试用例1：查找存在的元素
    test_cases = [
        (7, "查找元素 7（存在）"),
        (1, "查找元素 1（首位元素）"),
        (25, "查找元素 25（末位元素）"),
        (13, "查找元素 13（中间元素）"),
        (10, "查找元素 10（不存在）"),
        (0, "查找元素 0（小于最小值）"),
        (30, "查找元素 30（大于最大值）")
    ]
    
    print("1. 迭代版本二分查找:")
    print("-" * 50)
    for target, description in test_cases:
        result = binary_search(sorted_array, target)
        if result != -1:
            print(f"{description}")
            print(f"   结果: 找到了！索引位置 = {result}, 值 = {sorted_array[result]}")
        else:
            print(f"{description}")
            print(f"   结果: 未找到")
        print()
    
    print("=" * 50)
    print("2. 递归版本二分查找:")
    print("-" * 50)
    for target, description in test_cases:
        result = binary_search_recursive(sorted_array, target)
        if result != -1:
            print(f"{description}")
            print(f"   结果: 找到了！索引位置 = {result}, 值 = {sorted_array[result]}")
        else:
            print(f"{description}")
            print(f"   结果: 未找到")
        print()
    
    # 性能测试
    print("=" * 50)
    print("3. 性能对比测试:")
    print("-" * 50)
    import time
    
    # 创建一个大数组
    large_array = list(range(0, 1000000, 2))  # 50万个偶数
    target_value = 888888
    
    # 测试迭代版本
    start_time = time.time()
    for _ in range(1000):
        binary_search(large_array, target_value)
    iterative_time = time.time() - start_time
    
    # 测试递归版本
    start_time = time.time()
    for _ in range(1000):
        binary_search_recursive(large_array, target_value)
    recursive_time = time.time() - start_time
    
    print(f"数组大小: {len(large_array)} 个元素")
    print(f"迭代版本执行1000次耗时: {iterative_time:.6f} 秒")
    print(f"递归版本执行1000次耗时: {recursive_time:.6f} 秒")
    print()
    
    # 算法复杂度说明
    print("=" * 50)
    print("算法复杂度分析:")
    print("-" * 50)
    print("时间复杂度: O(log n)")
    print("空间复杂度: O(1) - 迭代版本")
    print("空间复杂度: O(log n) - 递归版本（因为递归调用栈）")
    print("=" * 50)
