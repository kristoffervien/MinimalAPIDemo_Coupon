using AutoMapper;
using FluentValidation;
using MagicVilla_CouponAPI;
using MagicVilla_CouponAPI.Data;
using MagicVilla_CouponAPI.Models;
using MagicVilla_CouponAPI.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;
using System.Runtime.CompilerServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAutoMapper(typeof(MappingConfig));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddResponseCaching((responseCachingOptions) =>
{
    responseCachingOptions.UseCaseSensitivePaths = true;
    responseCachingOptions.MaximumBodySize = 1024 * 1024; // 1MB

});
builder.Services.AddMemoryCache((memoryCachingOptions) =>
{
    // Example of setting up a size limit for the cache
    memoryCachingOptions.SizeLimit = 1024; // Size limit in bytes (or units depending on your implementation)
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCaching();

#region EndPoints

//---------------------------------------------------------------------------------------------------------
//GET: GetCoupons
//---------------------------------------------------------------------------------------------------------
app.MapGet("/api/coupon", (ILogger<Program> _logger, IMemoryCache _memoryCache) =>
{
    APIResponse response = new();

    var cacheCoupons = _memoryCache.Get("memoryCache_GetCoupons");

    if (cacheCoupons is null)
    {
        var coupons = CouponStore.couponList;

        response.Result = coupons;

        //memoryCache.Set("memoryCache_GetCoupons", coupons);

        //---------------------------------------------------------------------------------------------------------------------
        // We can also apply MemoryCacheEntryOptions
        //---------------------------------------------------------------------------------------------------------------------
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10), // Cache for 10 minutes max
            SlidingExpiration = TimeSpan.FromMinutes(2) // Reset expiration if accessed within 2 minutes
        };

        _memoryCache.Set("memoryCache_GetCoupons", coupons, cacheEntryOptions);
        //---------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------
    }
    else
    {
        response.Result = cacheCoupons;
    }

    _logger.Log(LogLevel.Information, "Getting all Coupons");
    response.IsSuccess = true;
    response.StatusCode = HttpStatusCode.OK;

    return Results.Ok(response);
}).WithName("GetCoupons").Produces<APIResponse>(200);
//---------------------------------------------------------------------------------------------------------

//---------------------------------------------------------------------------------------------------------
//GET: GetCoupon
//---------------------------------------------------------------------------------------------------------
app.MapGet("/api/coupon/{id:int}", (ILogger < Program> _logger, int id, HttpContext context) =>
{
    context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
    {
        Public = true,
        MaxAge = TimeSpan.FromSeconds(10)
    };

    context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] = new string[] { "Accept-Encoding" };

    APIResponse response = new();
    response.Result = CouponStore.couponList.FirstOrDefault(u => u.Id == id);
    response.IsSuccess = true;
    response.StatusCode = HttpStatusCode.OK;
    return Results.Ok(response);
}).WithName("GetCoupon").Produces<APIResponse>(200);
//---------------------------------------------------------------------------------------------------------

//---------------------------------------------------------------------------------------------------------
//POST: CreateCoupon
//---------------------------------------------------------------------------------------------------------
app.MapPost("/api/coupon", async (IMapper _mapper,
    IValidator<CouponCreateDTO> _validation, [FromBody] CouponCreateDTO coupon_C_DTO) =>
{
    APIResponse response = new() { IsSuccess = false, StatusCode = HttpStatusCode.BadRequest };

    var validationResult = await _validation.ValidateAsync(coupon_C_DTO);
    if (!validationResult.IsValid)
    {
        response.ErrorMessages.Add(validationResult.Errors.FirstOrDefault().ToString());
        return Results.BadRequest(response);
    }
    if (CouponStore.couponList.FirstOrDefault(u => u.Name.ToLower() == coupon_C_DTO.Name.ToLower()) != null)
    {
        response.ErrorMessages.Add("Coupon Name already Exists");
        return Results.BadRequest(response);
    }

    Coupon coupon = _mapper.Map<Coupon>(coupon_C_DTO);

    coupon.Id = CouponStore.couponList.OrderByDescending(u => u.Id).FirstOrDefault().Id + 1;
    CouponStore.couponList.Add(coupon);
    CouponDTO couponDTO = _mapper.Map<CouponDTO>(coupon);

    response.Result = couponDTO;
    response.IsSuccess = true;
    response.StatusCode = HttpStatusCode.Created;
    return Results.Ok(response);
    //return Results.CreatedAtRoute("GetCoupon",new { id=coupon.Id }, couponDTO);
    //return Results.Created($"/api/coupon/{coupon.Id}",coupon);
}).WithName("CreateCoupon").Accepts<CouponCreateDTO>("application/json").Produces<APIResponse>(201).Produces(400);
//---------------------------------------------------------------------------------------------------------

//---------------------------------------------------------------------------------------------------------
//PUT: UpdateCoupon
//---------------------------------------------------------------------------------------------------------
app.MapPut("/api/coupon", async (IMapper _mapper,
    IValidator<CouponUpdateDTO> _validation, [FromBody] CouponUpdateDTO coupon_U_DTO) =>
{
    APIResponse response = new() { IsSuccess = false, StatusCode = HttpStatusCode.BadRequest };

    var validationResult = await _validation.ValidateAsync(coupon_U_DTO);
    if (!validationResult.IsValid)
    {
        response.ErrorMessages.Add(validationResult.Errors.FirstOrDefault().ToString());
        return Results.BadRequest(response);
    }

    Coupon couponFromStore = CouponStore.couponList.FirstOrDefault(u => u.Id == coupon_U_DTO.Id);
    couponFromStore.IsActive = coupon_U_DTO.IsActive;
    couponFromStore.Name = coupon_U_DTO.Name;
    couponFromStore.Percent = coupon_U_DTO.Percent;
    couponFromStore.LastUpdated = DateTime.Now;

    response.Result = _mapper.Map<CouponDTO>(couponFromStore); ;
    response.IsSuccess = true;
    response.StatusCode = HttpStatusCode.OK;
    return Results.Ok(response);
}).WithName("UpdateCoupon")
    .Accepts<CouponUpdateDTO>("application/json").Produces<APIResponse>(200).Produces(400);
//---------------------------------------------------------------------------------------------------------

//---------------------------------------------------------------------------------------------------------
//DELETE:
//---------------------------------------------------------------------------------------------------------
app.MapDelete("/api/coupon/{id:int}", (int id) =>
{
    APIResponse response = new() { IsSuccess = false, StatusCode = HttpStatusCode.BadRequest };

    Coupon couponFromStore = CouponStore.couponList.FirstOrDefault(u => u.Id == id);
    if (couponFromStore != null)
    {
        CouponStore.couponList.Remove(couponFromStore);
        response.IsSuccess = true;
        response.StatusCode = HttpStatusCode.NoContent;
        return Results.Ok(response);
    }
    else
    {
        response.ErrorMessages.Add("Invalid Id");
        return Results.BadRequest(response);
    }
});
//---------------------------------------------------------------------------------------------------------

#endregion EndPoints

app.UseHttpsRedirection();

app.Run();