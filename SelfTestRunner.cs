using System.Text.Json;
using ModelContextProtocol;

namespace KatLedger;

internal static class SelfTestRunner
{
    public static void Run(KatLedgerStore store)
    {
        string workspace = $"self-test::{Environment.MachineName}";
        string taskId = $"katledger-{Guid.NewGuid():N}";

        try
        {
            store.DeleteChecks(workspace, taskId);

            InsertCheckResult baseline = KatLedgerTools.InsertCheck(
                workspace,
                taskId,
                "baseline",
                "build",
                "dotnet",
                JsonSerializer.SerializeToElement(true),
                "dotnet build",
                0,
                "Build succeeded");

            _ = KatLedgerTools.InsertCheck(
                workspace,
                taskId,
                "after",
                "tests",
                "dotnet",
                JsonSerializer.SerializeToElement(0),
                "dotnet test",
                1,
                "One test failed");

            CountChecksResult baselineCount = KatLedgerTools.CountChecks(workspace, taskId, phase: "baseline");
            if (baselineCount.Count != 1)
            {
                throw new InvalidOperationException("Baseline count self-test failed.");
            }

            ListChecksResult recentChecks = KatLedgerTools.ListChecks(workspace, taskId, limit: 10);
            if (recentChecks.Returned != 2 || recentChecks.Items[0].Id <= recentChecks.Items[1].Id)
            {
                throw new InvalidOperationException("List ordering self-test failed.");
            }

            ReadChecksResult allChecks = KatLedgerTools.ReadChecks(workspace, taskId, limit: 10);
            if (allChecks.Returned != 2 || allChecks.Items[0].Id != baseline.Id)
            {
                throw new InvalidOperationException("Read ordering self-test failed.");
            }

            string oversizedSnippet = new('x', KatLedgerStore.MaxOutputSnippetLength + 1);
            try
            {
                _ = KatLedgerTools.InsertCheck(
                    workspace,
                    taskId,
                    "review",
                    "review-self",
                    "anvil",
                    JsonSerializer.SerializeToElement(true),
                    output_snippet: oversizedSnippet);

                throw new InvalidOperationException("Output snippet validation self-test failed.");
            }
            catch (McpException)
            {
            }
        }
        finally
        {
            store.DeleteChecks(workspace, taskId);
        }
    }
}
