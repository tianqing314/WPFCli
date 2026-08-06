using Microsoft.EntityFrameworkCore;
using TESTRIG.Core.Abstractions;

namespace TESTRIG.Infrastructure.Data;

/// <summary>
/// 把一次测试会话结果写入结果库的共用逻辑（本地 SQLite 与远程 MySQL 复用同一套映射规则）。
/// </summary>
public static class ResultWriter
{
    /// <summary>
    /// 保存会话结果：子表逐项写；全测（FullRun）且有 SN 时主表按 SN upsert（首测时间取最早、末测时间取最晚）。
    /// 只装载实体，不提交——由调用方决定何时 <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>。
    /// </summary>
    /// <param name="db">结果库上下文。</param>
    /// <param name="result">测试会话结果。</param>
    /// <param name="ct">取消令牌。</param>
    public static async Task WriteSessionAsync(ResultDbContextBase db, TestSessionResult result, CancellationToken ct = default)
    {
        foreach (var pos in result.Positions)
        {
            var sn = pos.SerialNumber ?? "";

            // 子表：每个测试项一行（SN + 测试项维度）
            foreach (var rec in pos.Steps)
            {
                db.TestDataDetails.Add(new PcbaTestDataDetail
                {
                    TaskId = result.TaskId,
                    DeviceSn = sn,
                    TestItemCode = rec.Step.Key,
                    TestItemName = rec.Step.Name,
                    TestProcessInfos = rec.ProcessInfos,
                    TestProcessData = rec.ProcessData,
                    ResultStatus = rec.Result.Status.ToString(),
                    ErrorMessage = rec.Result.IsPass ? null : (rec.Result.Detail ?? rec.Result.Summary),
                    StartTime = rec.StartedAt,
                    EndTime = rec.FinishedAt,
                    Operator = result.Operator,
                });
            }

            // 主表：仅跑全部测试项时，按 SN 记录或更新
            if (result.FullRun && !string.IsNullOrEmpty(sn))
            {
                var posStart = pos.Steps.Count > 0 ? pos.Steps.Min(s => s.StartedAt) : result.StartedAt;
                var posEnd = pos.Steps.Count > 0 ? pos.Steps.Max(s => s.FinishedAt) : result.FinishedAt;

                var main = await db.TestData.FirstOrDefaultAsync(x => x.DeviceSn == sn, ct);
                if (main is null)
                {
                    main = new PcbaTestData { DeviceSn = sn, StartTime = posStart };  // 第一次开始时间
                    db.TestData.Add(main);
                }

                main.BatchNo = result.BatchNo;
                main.DeviceModel = result.DeviceModel;
                main.StationNo = result.StationNo;
                main.IsPass = pos.Passed;
                // 重压重测会话 → 标记该 SN 已重压；一旦标记保持为真（首测不清除）
                main.IsRePressed |= result.IsRePress;
                main.Operator = result.Operator;
                if (posEnd > main.EndTime)
                {
                    main.EndTime = posEnd;                     // 最后一次结束时间
                }
            }
        }
    }
}
