using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Dtos.Statistic;
using server.Enums;
using server.Interfaces.Repositories;
using server.Models;
using server.Queries;
using server.Utilities;

namespace server.Repositories
{
    public class RoomRepository : IRoomRepository
    {
        private readonly ApplicationDBContext _dbContext;

        public RoomRepository(ApplicationDBContext context)
        {
            _dbContext = context;
        }

        private IQueryable<Room> ApplyFilters(IQueryable<Room> query, Dictionary<string, object> filters)
        {
            foreach (var filter in filters)
            {
                string value = filter.Value.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(value))
                {
                    switch (filter.Key)
                    {
                        case "startTime":
                            query = query.Where(rm => rm.CreatedAt >= DateTime.Parse(value));
                            break;
                        case "endTime":
                            query = query.Where(rm => rm.CreatedAt <= TimestampHandler.GetEndOfTimeByType(DateTime.Parse(value), "daily"));
                            break;
                        case "roomNumber":
                            query = query.Where(rm => rm.RoomNumber.Contains(value));
                            break;
                        case "status":
                            query = query.Where(rm => rm.Status == Enum.Parse<RoomStatus>(value));
                            break;
                        case "minPrice":
                            query = query.Where(rm => rm.RoomClass!.BasePrice >= Convert.ToDecimal(value));
                            break;
                        case "maxPrice":
                            query = query.Where(rm => rm.RoomClass!.BasePrice <= Convert.ToDecimal(value));
                            break;
                        case "isAvailable":
                            query = query.Where(rm =>
                                value == "1" ? rm.Status != RoomStatus.OutOfService : rm.Status == RoomStatus.OutOfService
                            );
                            break;
                        case "features":
                            var featureIds = JsonSerializer.Deserialize<List<int>>(filter.Value.ToString() ?? "[]");
                            query = query.Where(rm =>
                                featureIds!.All(featureId => rm.RoomClass!.RoomClassFeatures.Any(rcf => rcf.FeatureId == featureId))
                            );
                            break;
                        default:
                            query = query.Where(rm => EF.Property<string>(rm, filter.Key.CapitalizeWord()) == value);
                            break;
                    }
                }
            }

            return query;
        }

        private IQueryable<Room> ApplySorting(IQueryable<Room> query, Dictionary<string, string> sort)
        {
            foreach (var order in sort)
            {
                if (order.Key == "price")
                {
                    query =
                        order.Value == "ASC"
                            ? query.OrderBy(mt => mt.RoomClass!.BasePrice)
                            : query.OrderByDescending(mt => mt.RoomClass!.BasePrice);
                }
                else
                {
                    query =
                        order.Value == "ASC"
                            ? query.OrderBy(mt => EF.Property<object>(mt, order.Key.CapitalizeWord()))
                            : query.OrderByDescending(mt => EF.Property<object>(mt, order.Key.CapitalizeWord()));
                }
            }

            return query;
        }

        public async Task<(List<Room>, int)> GetAllRooms(BaseQueryObject queryObject)
        {
            var query = _dbContext
                .Rooms.Include(rm => rm.Floor)
                .Include(rm => rm.CreatedBy)
                .Include(rm => rm.Images)
                .Include(rm => rm.RoomClass)
                .ThenInclude(rc => rc!.RoomClassFeatures)
                .ThenInclude(rcf => rcf.Feature)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(queryObject.Filter))
            {
                var parsedFilter = JsonSerializer.Deserialize<Dictionary<string, object>>(queryObject.Filter);
                query = ApplyFilters(query, parsedFilter!);
            }

            if (!string.IsNullOrWhiteSpace(queryObject.Sort))
            {
                var parsedSort = JsonSerializer.Deserialize<Dictionary<string, string>>(queryObject.Sort);
                query = ApplySorting(query, parsedSort!);
            }

            var total = await query.CountAsync();

            if (queryObject.Skip.HasValue)
                query = query.Skip(queryObject.Skip.Value);

            if (queryObject.Limit.HasValue)
                query = query.Take(queryObject.Limit.Value);

            var rooms = await query.ToListAsync();

            return (rooms, total);
        }

        public async Task<Room?> GetRoomById(int roomId)
        {
            return await _dbContext
                .Rooms.Include(rm => rm.Floor)
                .Include(rm => rm.CreatedBy)
                .Include(rm => rm.Images)
                .Include(rm => rm.RoomClass)
                .ThenInclude(rc => rc!.RoomClassFeatures)
                .ThenInclude(rcf => rcf.Feature)
                .Where(rm => rm.Id == roomId)
                .FirstOrDefaultAsync();
        }

        public async Task<Room?> GetRoomByRoomNumber(string roomNumber)
        {
            return await _dbContext.Rooms.Where(rm => rm.RoomNumber == roomNumber).FirstOrDefaultAsync();
        }

        public async Task CreateNewRoom(Room room)
        {
            _dbContext.Rooms.Add(room);
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateRoom(Room room)
        {
            _dbContext.Rooms.Update(room);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteRoom(Room room)
        {
            _dbContext.Rooms.Remove(room);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<int> CountBookedTimes(int roomId)
        {
            return await _dbContext.BookingRooms.Where(bkr => bkr.RoomId == roomId).CountAsync();
        }

        public async Task<bool> CheckIfRoomIsBooked(int roomId)
        {
            var upcomingBookingRoomIds = await _dbContext
                .Bookings.Where(bk => bk.Status != BookingStatus.Cancelled && bk.CheckInTime >= DateTime.Now.Date)
                .SelectMany(bk => bk.BookingRooms.Select(br => br.RoomId))
                .Distinct()
                .ToListAsync();

            return upcomingBookingRoomIds.Any(id => id == roomId);
        }

        public async Task DeleteOldImagesOfRoom(int roomId)
        {
            var images = await _dbContext.RoomImages.Where(ri => ri.RoomId == roomId).ToListAsync();

            _dbContext.RoomImages.RemoveRange(images);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<int> GetRoomStatisticInTimeRange(DateTime startTime, DateTime endTime, int roomId)
        {
            return await _dbContext
                .BookingRooms.Where(bkr =>
                    bkr.Booking!.CheckInTime >= startTime
                    && bkr.Booking!.CheckInTime < endTime
                    && (
                        bkr.Booking!.Status == BookingStatus.CheckedIn
                        || bkr.Booking!.Status == BookingStatus.CheckedOut
                        || bkr.Booking!.Status == BookingStatus.PaymentDone
                    )
                    && bkr.RoomId == roomId
                )
                .CountAsync();
        }

        public async Task<List<RoomWithBookingCount>> GetMostBookedRoomsInTimeRange(DateTime startTime, DateTime endTime, int limit)
        {
            var mostBookedRoomIds = await _dbContext
                .BookingRooms.Where(bkr =>
                    bkr.Booking!.CheckInTime >= startTime
                    && bkr.Booking!.CheckInTime < endTime
                    && (
                        bkr.Booking!.Status == BookingStatus.CheckedIn
                        || bkr.Booking!.Status == BookingStatus.CheckedOut
                        || bkr.Booking!.Status == BookingStatus.PaymentDone
                    )
                )
                .GroupBy(bkr => bkr.RoomId)
                .Select(g => new { RoomId = g.Key, BookingCount = g.Count() })
                .OrderByDescending(x => x.BookingCount)
                .Take(limit)
                .Select(x => new { x.RoomId, x.BookingCount })
                .ToListAsync();

            List<RoomWithBookingCount> result = [];
            foreach (var item in mostBookedRoomIds)
            {
                var roomInfo = await _dbContext
                    .Rooms.Include(rm => rm.Floor)
                    .Include(rm => rm.CreatedBy)
                    .Include(rm => rm.Images)
                    .Include(rm => rm.RoomClass)
                    .Where(rm => rm.Id == item.RoomId)
                    .FirstOrDefaultAsync();

                if (roomInfo != null)
                {
                    result.Add(new RoomWithBookingCount(roomInfo, item.BookingCount));
                }
            }

            return result;
        }
    }
}
