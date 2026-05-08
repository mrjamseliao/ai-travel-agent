using System.Text.RegularExpressions;
using AiTravelAgent.Models;

namespace AiTravelAgent.Services;

public class KnowledgeGraphService
{
    public static KnowledgeGraphData BuildKnowledgeGraph(TripPlan tripPlan, string language = "zh")
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var categories = new List<GraphCategory>
        {
            new GraphCategory { Name = "景点" },
            new GraphCategory { Name = "餐饮" },
            new GraphCategory { Name = "酒店" }
        };

        var nodeIdMap = new Dictionary<string, string>();
        var idCounter = 1;

        foreach (var day in tripPlan.Days)
        {
            foreach (var attraction in day.Attractions)
            {
                var nodeId = $"attraction_{idCounter++}";
                nodeIdMap[attraction.Name] = nodeId;

                nodes.Add(new GraphNode
                {
                    Id = nodeId,
                    Name = attraction.Name,
                    Category = 0,
                    SymbolSize = 40,
                    Value = $"{attraction.Category ?? "景点"}|{attraction.TicketPrice}元"
                });
            }

            foreach (var meal in day.Meals)
            {
                var nodeId = $"meal_{idCounter++}";
                nodeIdMap[meal.Name] = nodeId;

                nodes.Add(new GraphNode
                {
                    Id = nodeId,
                    Name = meal.Name,
                    Category = 1,
                    SymbolSize = 25,
                    Value = $"{meal.Type}|{meal.EstimatedCost}元"
                });
            }

            if (day.Hotel != null)
            {
                var nodeId = $"hotel_{idCounter++}";
                nodeIdMap[day.Hotel.Name] = nodeId;

                nodes.Add(new GraphNode
                {
                    Id = nodeId,
                    Name = day.Hotel.Name,
                    Category = 2,
                    SymbolSize = 35,
                    Value = $"{day.Hotel.Type}|{day.Hotel.PriceRange}"
                });
            }
        }

        foreach (var day in tripPlan.Days)
        {
            var orderedItems = new List<(string name, int order)>();

            for (int i = 0; i < day.Attractions.Count; i++)
                orderedItems.Add((day.Attractions[i].Name, i * 2));

            for (int i = 0; i < day.Meals.Count; i++)
                orderedItems.Add((day.Meals[i].Name, i * 2 + 1));

            orderedItems = orderedItems.OrderBy(x => x.order).ToList();

            for (int i = 0; i < orderedItems.Count - 1; i++)
            {
                var sourceId = nodeIdMap.GetValueOrDefault(orderedItems[i].name);
                var targetId = nodeIdMap.GetValueOrDefault(orderedItems[i + 1].name);

                if (!string.IsNullOrEmpty(sourceId) && !string.IsNullOrEmpty(targetId) && sourceId != targetId)
                {
                    edges.Add(new GraphEdge
                    {
                        Source = sourceId,
                        Target = targetId,
                        Label = orderedItems[i + 1].order % 2 == 0 ? "游览" : "用餐"
                    });
                }
            }
        }

        return new KnowledgeGraphData
        {
            Nodes = nodes,
            Edges = edges,
            Categories = categories
        };
    }
}
