using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace WebhookService.Core.DTOs;

public class CreateEventRequest
{
    [Required]
    [MaxLength(128)]
    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    [Required]
    public JsonElement Payload { get; set; }
}
