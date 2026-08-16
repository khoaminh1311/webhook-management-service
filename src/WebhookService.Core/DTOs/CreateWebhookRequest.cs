using System.ComponentModel.DataAnnotations;

namespace WebhookService.Core.DTOs;

public class CreateWebhookRequest
{
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Url]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "At least one event type is required.")]
    public List<string> EventTypes { get; set; } = new List<string>();
}
