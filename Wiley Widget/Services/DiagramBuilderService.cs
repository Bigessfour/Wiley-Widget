using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Syncfusion.UI.Xaml.Diagram;
using WileyWidget.Models;

namespace WileyWidget.Services
{
    /// <summary>
    /// Pure builder for diagram nodes and connectors representing Enterprises and BudgetInteractions.
    /// Keeps layout + mapping logic out of the view.
    /// </summary>
    public interface IDiagramBuilderService
    {
        (IEnumerable<Node> nodes, IEnumerable<Connector> connectors) BuildEnterpriseDiagram(
            IReadOnlyCollection<Enterprise> enterprises,
            IReadOnlyCollection<BudgetInteraction> interactions,
            double startX = 100, double startY = 100,
            double xStep = 200, double yStep = 150, double wrapX = 600);
    }

    public sealed class DiagramBuilderService : IDiagramBuilderService
    {
        public (IEnumerable<Node> nodes, IEnumerable<Connector> connectors) BuildEnterpriseDiagram(
            IReadOnlyCollection<Enterprise> enterprises,
            IReadOnlyCollection<BudgetInteraction> interactions,
            double startX = 100, double startY = 100,
            double xStep = 200, double yStep = 150, double wrapX = 600)
        {
            var nodes = new List<Node>();
            var connectors = new List<Connector>();
            if (enterprises == null || enterprises.Count == 0) return (nodes, connectors);

            var map = new Dictionary<int, Node>();
            double x = startX;
            double y = startY;

            foreach (var e in enterprises)
            {
                try
                {
                    var node = new Node
                    {
                        Content = $"{e.Name}\n${e.MonthlyRevenue:F0} Rev\n${e.MonthlyExpenses:F0} Exp",
                        Width = 150,
                        Height = 80,
                        OffsetX = x,
                        OffsetY = y
                    };
                    nodes.Add(node);
                    if (e.Id != 0) map[e.Id] = node;
                    x += xStep;
                    if (x > wrapX) { x = startX; y += yStep; }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to build node for enterprise {Name}", e?.Name);
                }
            }

            if (interactions != null && interactions.Count > 0)
            {
                foreach (var interaction in interactions)
                {
                    try
                    {
                        if (!map.TryGetValue(interaction.PrimaryEnterpriseId, out var source)) continue;
                        if (interaction.SecondaryEnterpriseId.HasValue &&
                            map.TryGetValue(interaction.SecondaryEnterpriseId.Value, out var target))
                        {
                            var connector = new Connector
                            {
                                SourceNode = source,
                                TargetNode = target,
                                Content = $"{interaction.InteractionType}\n${interaction.MonthlyAmount:F0}"
                            };
                            connectors.Add(connector);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to build connector for interaction {Type}", interaction?.InteractionType);
                    }
                }
            }

            return (nodes, connectors);
        }
    }
}
