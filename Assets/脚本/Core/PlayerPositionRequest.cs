/// <summary>
/// 事件载荷：请求玩家当前位置。
/// 发射方（HomingBulletEmitter 等）创建并广播 "RequestPlayerPosition" 事件，
/// 玩家端（PlayerKongzhi）填写 Position 并标记 HasPosition = true。
/// 若无人填写（玩家禁用/不存在），发射方自行使用回退方向。
/// </summary>
using UnityEngine;
public class PlayerPositionRequest
{
    /// <summary>玩家当前位置（世界坐标），由玩家填写</summary>
    public Vector3 Position;

    /// <summary>是否成功获取到位置</summary>
    public bool HasPosition;
}
