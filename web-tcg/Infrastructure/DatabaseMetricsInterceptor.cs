using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prometheus;

namespace web_tcg.Infrastructure;

/// <summary>Aggregate timings only: no SQL text, parameters, user IDs or connection strings.</summary>
public sealed class DatabaseMetricsInterceptor : DbCommandInterceptor
{
    private static readonly Counter Commands = Metrics.CreateCounter(
        "worlds_tcg_db_commands_total", "Database commands",
        new CounterConfiguration { LabelNames = new[] { "operation", "status" } });
    private static readonly Histogram Duration = Metrics.CreateHistogram(
        "worlds_tcg_db_command_duration_seconds", "Database command duration",
        new HistogramConfiguration {
            LabelNames = new[] { "operation" },
            Buckets = new[] { .001, .005, .01, .025, .05, .1, .25, .5, 1, 2.5, 5 }
        });

    private static void Record(CommandEndEventData data, string status)
    {
        var operation = data.ExecuteMethod.ToString();
        Commands.WithLabels(operation, status).Inc();
        Duration.WithLabels(operation).Observe(data.Duration.TotalSeconds);
    }
    public override DbDataReader ReaderExecuted(DbCommand command,CommandExecutedEventData data,DbDataReader result) {Record(data,"success");return result;}
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default) {Record(data,"success");return ValueTask.FromResult(result);}
    public override int NonQueryExecuted(DbCommand command,CommandExecutedEventData data,int result) {Record(data,"success");return result;}
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command,CommandExecutedEventData data,int result,CancellationToken ct=default) {Record(data,"success");return ValueTask.FromResult(result);}
    public override object? ScalarExecuted(DbCommand command,CommandExecutedEventData data,object? result) {Record(data,"success");return result;}
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command,CommandExecutedEventData data,object? result,CancellationToken ct=default) {Record(data,"success");return ValueTask.FromResult(result);}
    public override void CommandFailed(DbCommand command,CommandErrorEventData data) => Record(data,"error");
    public override Task CommandFailedAsync(DbCommand command,CommandErrorEventData data,CancellationToken ct=default) {Record(data,"error");return Task.CompletedTask;}
}
