using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace server.Models
{
    public class Floor
    {
        public int Id { get; set; }
        public string FloorNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        public Admin? CreatedBy { get; set; }
        public List<Room> Rooms { get; set; } = [];
    }
}
