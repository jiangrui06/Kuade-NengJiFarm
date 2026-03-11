using System;
using System.Collections.Generic;

namespace WebApplication1.Models
{
    public record PagedResult<T>
    {
        public int PageIndex { get; init; }
        public int PageSize { get; init; }
        public int Total { get; init; }
        public IEnumerable<T>? Items { get; init; }
    }

    public record UserDto
    {
        public Guid Id { get; init; }
        public string? NickName { get; init; }
        public string? AvatarUrl { get; init; }
        public string? PhoneNumber { get; init; }
    }

    public record GoodsDto
    {
        public Guid Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public decimal Price { get; init; }
        public string? ImageUrl { get; init; }
    }

    public record AcreDto
    {
        public Guid Id { get; init; }
        public string? Name { get; init; }
        public string? Status { get; init; }
        public string? Description { get; init; }
    }

    public record CartItemDto
    {
        public Guid Id { get; init; }
        public GoodsDto? Goods { get; init; }
        public int Quantity { get; init; }
    }

    public record OrderDto
    {
        public Guid Id { get; init; }
        public decimal TotalAmount { get; init; }
        public string? Status { get; init; }
    }

    public record ActivityDto
    {
        public Guid Id { get; init; }
        public string? Title { get; init; }
        public string? Content { get; init; }
    }
}
