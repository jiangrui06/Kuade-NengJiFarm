using System.ComponentModel.DataAnnotations;

namespace WebAPI.Dtos;

public class SwiperItemDto
{
    public int Id { get; set; }

    public string Image { get; set; } = string.Empty;
}

public class FunctionButtonDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

public class CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;
}

public class GoodsSummaryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal OriginalPrice { get; set; }

    public int Stock { get; set; }

    public List<string> Tags { get; set; } = [];
}

public class HomeIndexDto
{
    public List<SwiperItemDto> SwiperList { get; set; } = [];

    public List<FunctionButtonDto> FunctionButtons { get; set; } = [];

    public List<GoodsSummaryDto> FarmGoods { get; set; } = [];

    public List<GoodsSummaryDto> HotDishes { get; set; } = [];
}

public class FarmGoodsIndexDto
{
    public List<SwiperItemDto> SwiperList { get; set; } = [];

    public List<CategoryDto> Categories { get; set; } = [];

    public List<GoodsSummaryDto> TodayGoods { get; set; } = [];

    public List<GoodsSummaryDto> HotGoods { get; set; } = [];
}

public class PagedGoodsDto
{
    public List<GoodsSummaryDto> GoodsList { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}

public class GoodsDetailDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Image { get; set; } = string.Empty;

    public string DetailImage { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Weight { get; set; } = string.Empty;

    public string Storage { get; set; } = string.Empty;

    public int Stock { get; set; }

    public List<string> Tags { get; set; } = [];
}

public class CartItemDto
{
    public int Id { get; set; }

    public int GoodsId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Count { get; set; }

    public bool Checked { get; set; }
}

public class CartListDto
{
    public List<CartItemDto> CartList { get; set; } = [];
}

public class CartAddRequest
{
    [Required]
    public int GoodsId { get; set; }

    [Range(1, int.MaxValue)]
    public int Count { get; set; }
}

public class CartUpdateRequest
{
    [Required]
    public int CartId { get; set; }

    [Range(1, int.MaxValue)]
    public int Count { get; set; }
}

public class CartDeleteRequest
{
    [Required]
    public int CartId { get; set; }
}


public class UserProfileDto
{
    public int Id { get; set; }

    public string Nickname { get; set; } = string.Empty;

    public string Avatar { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public class UpdateUserProfileRequest
{
    public string? Nickname { get; set; }

    public string? Avatar { get; set; }

    public string? Email { get; set; }
}

public class AddressDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}

public class SaveAddressRequest
{
    public int? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public string Province { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string District { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}

public class DeleteAddressRequest
{
    [Required]
    public int Id { get; set; }
}
