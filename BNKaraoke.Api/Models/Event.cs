using System;
using System.ComponentModel.DataAnnotations;
namespace BNKaraoke.Api.Models;
   public class Event
    {
        public int Id { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;
    }

