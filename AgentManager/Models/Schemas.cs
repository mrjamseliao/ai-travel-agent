using System.ComponentModel.DataAnnotations;

namespace AiTravelAgent.Models;

public class TripRequest
{
    [Required]
    public string City { get; set; } = "";

    [Required]
    public string StartDate { get; set; } = "";

    [Required]
    public string EndDate { get; set; } = "";

    [Range(1, 30)]
    public int TravelDays { get; set; }

    [Required]
    public string Transportation { get; set; } = "";

    [Required]
    public string Accommodation { get; set; } = "";

    public List<string> Preferences { get; set; } = new();

    public string? FreeTextInput { get; set; } = "";

    public string? Language { get; set; } = "zh";
}

public class POISearchRequest
{
    [Required]
    public string Keywords { get; set; } = "";

    [Required]
    public string City { get; set; } = "";

    public bool CityLimit { get; set; } = true;
}

public class RouteRequest
{
    [Required]
    public string OriginAddress { get; set; } = "";

    [Required]
    public string DestinationAddress { get; set; } = "";

    public string? OriginCity { get; set; }

    public string? DestinationCity { get; set; }

    public string RouteType { get; set; } = "walking";
}

public class Location
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
}

public class Attraction
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public Location Location { get; set; } = new();
    public int VisitDuration { get; set; }
    public string Description { get; set; } = "";
    public string? Category { get; set; } = "景点";
    public double? Rating { get; set; }
    public List<string> Photos { get; set; } = new();
    public string PoiId { get; set; } = "";
    public string? ImageUrl { get; set; }
    public int TicketPrice { get; set; }
    public bool? ReservationRequired { get; set; }
    public string? ReservationTips { get; set; } = "";
}

public class Meal
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public Location? Location { get; set; }
    public string? Description { get; set; }
    public int EstimatedCost { get; set; }
}

public class Hotel
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public Location? Location { get; set; }
    public string PriceRange { get; set; } = "";
    public string Rating { get; set; } = "";
    public string Distance { get; set; } = "";
    public string Type { get; set; } = "";
    public int EstimatedCost { get; set; }
}

public class DayPlan
{
    public string Date { get; set; } = "";
    public int DayIndex { get; set; }
    public string Description { get; set; } = "";
    public string Transportation { get; set; } = "";
    public string Accommodation { get; set; } = "";
    public Hotel? Hotel { get; set; }
    public List<Attraction> Attractions { get; set; } = new();
    public List<Meal> Meals { get; set; } = new();
}

public class WeatherInfo
{
    public string Date { get; set; } = "";
    public string DayWeather { get; set; } = "";
    public string NightWeather { get; set; } = "";
    public object DayTemp { get; set; } = 0;
    public object NightTemp { get; set; } = 0;
    public string WindDirection { get; set; } = "";
    public string WindPower { get; set; } = "";
}

public class Budget
{
    public int TotalAttractions { get; set; }
    public int TotalHotels { get; set; }
    public int TotalMeals { get; set; }
    public int TotalTransportation { get; set; }
    public int Total { get; set; }
}

public class TripPlan
{
    public string City { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public List<DayPlan> Days { get; set; } = new();
    public List<WeatherInfo> WeatherInfo { get; set; } = new();
    public string OverallSuggestions { get; set; } = "";
    public Budget? Budget { get; set; }
}

public class GraphNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Category { get; set; }
    public int SymbolSize { get; set; } = 30;
    public object? ItemStyle { get; set; }
    public string Value { get; set; } = "";
}

public class GraphEdge
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Label { get; set; } = "";
}

public class GraphCategory
{
    public string Name { get; set; } = "";
}

public class KnowledgeGraphData
{
    public List<GraphNode> Nodes { get; set; } = new();
    public List<GraphEdge> Edges { get; set; } = new();
    public List<GraphCategory> Categories { get; set; } = new();
}

public class TripPlanResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? PlanId { get; set; }
    public TripPlan? Data { get; set; }
    public KnowledgeGraphData? GraphData { get; set; }
}

public class POIInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Address { get; set; } = "";
    public Location Location { get; set; } = new();
    public string? Tel { get; set; }
}

public class POISearchResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<POIInfo> Data { get; set; } = new();
}

public class RouteInfo
{
    public double Distance { get; set; }
    public int Duration { get; set; }
    public string RouteType { get; set; } = "";
    public string Description { get; set; } = "";
}

public class RouteResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public RouteInfo? Data { get; set; }
}

public class WeatherResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<WeatherInfo> Data { get; set; } = new();
}

public class ErrorResponse
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = "";
    public string? ErrorCode { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class TripChatRequest
{
    [Required]
    public string Message { get; set; } = "";

    [Required]
    public Dictionary<string, object> TripPlan { get; set; } = new();

    public List<ChatMessage>? History { get; set; }
}

public class TripChatResponse
{
    public bool Success { get; set; } = true;
    public string Reply { get; set; } = "";
}
