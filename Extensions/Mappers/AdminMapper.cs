using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using server.Dtos.Auth;
using server.Models;

namespace server.Extensions.Mappers
{
    public static class AdminMapper
    {
        public static AdminDto ToAdminDto(this Admin admin)
        {
            return new AdminDto
            {
                Id = admin.Id,
                FirstName = admin.FirstName,
                LastName = admin.LastName,
                Email = admin.Email,
                Avatar = admin.Avatar,
                PhoneNumber = admin.PhoneNumber,
                CreatedAt = admin.CreatedAt,
                CreatedById = admin.CreatedBy == null ? null : admin.CreatedBy.Id,
                CreatedBy = admin.CreatedBy == null ? null : $"{admin.CreatedBy.LastName} {admin.CreatedBy.FirstName}",
                IsActive = admin.Account != null && admin.Account.IsActive,
            };
        }
    }
}
