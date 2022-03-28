using System.Globalization;
using System.Text;
using complexcart.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace complexcart.Controllers;

[ApiController]
[Route("[controller]")]
public class CartController : Controller
{
    private static readonly string AuthUrl = Environment.GetEnvironmentVariable("AUTH_URL") ?? "";
    private static readonly string CartUrl = Environment.GetEnvironmentVariable("CART_URL") ?? "";
    private static readonly string InventoryUrl = Environment.GetEnvironmentVariable("INVENTORY_URL") ?? "";

    // GET
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAllItemFromCart(string userId)
    {
        if (!await CheckUser(userId))
        {
            return Unauthorized();
        }

        using var client = new HttpClient();
        var result = await client.GetAsync($"{CartUrl}/cart/{userId}");
        var responseContent = await result.Content.ReadAsStringAsync();
        var cartItems = JsonConvert.DeserializeObject<IEnumerable<Cart>>(responseContent);
        var cartViewModel = new List<CartViewModel>();
        if (cartItems == null)
        {
            return Ok(cartViewModel);
            
        }

        if (cartItems.Count() == 0)
        {
            return Ok(cartViewModel);
        }
        
        foreach (var cart in cartItems)
        {
            var i = await GetItem(cart.ItemId);
            bool isRemoved = false;
            if (i == null)
            {
                await client.DeleteAsync($"{CartUrl}/cart/{userId}/{i.ItemId}");
                isRemoved = true;
            }

            if (!isRemoved && cart.Quantity > i.Quantity)
            {
                await client.DeleteAsync($"{CartUrl}/cart/{userId}/{i.ItemId}");
                isRemoved = true;
            }
            
            if (!isRemoved && !i.IsAvailable)
            {
                await client.DeleteAsync($"{CartUrl}/cart/{userId}/{i.ItemId}");
                isRemoved = true;
            }
            cartViewModel.Add(new CartViewModel(i.Name, i.ItemId, i.PricePerItem, cart.Quantity, i.Url, isRemoved));
        }

        return Ok(cartViewModel);
    }
    
    [HttpPost("{userId}")]
    public async Task<IActionResult> AddItemToCart(string userId, [FromBody ]Item item)
    {
        if (!await CheckUser(userId))
        {
            return Unauthorized();
        }
        if (item.Quantity <= 0)
        {
            return BadRequest("Value of item cannot be below or equals to 0");
        }
        var i = await GetItem(item.ItemId);
        if (i == null)
        {
            return NotFound("Item not found");
        }

        if (!i.IsAvailable)
        {
            return NotFound("Item not found");
        }

        if (i.Quantity < item.Quantity)
        {
            return BadRequest("Item does not have enough quantity");
        }
        using var client = new HttpClient();

        var result = await client.GetAsync($"{CartUrl}/cart/{userId}");
        var responseContent = await result.Content.ReadAsStringAsync();
        var cartItems = JsonConvert.DeserializeObject<IEnumerable<Cart>>(responseContent);
        if (cartItems != null)
        {
            var cartItem = cartItems.FirstOrDefault();
            if (cartItem != null)
            {
                return await UpdateItemInCart(userId, item.ItemId,
                    new QuantityModel(cartItem.Quantity + item.Quantity));
            }
        }
        
        var json = JsonConvert.SerializeObject(item);
        var data = new StringContent(json, Encoding.UTF8, "application/json");
        
        await client.PostAsync($"{CartUrl}/cart/{userId}",data);
        
        return Ok();
    }
    
    [HttpPut("{userId}/{itemId}")]
    public async Task<IActionResult> UpdateItemInCart(string userId, string itemId, [FromBody ]QuantityModel q)
    {
        if (!await CheckUser(userId))
        {
            return Unauthorized();
        }
        
        using var client = new HttpClient();

        if (q.Quantity == 0)
        {
            //delete item
            await client.DeleteAsync($"{CartUrl}/cart/{userId}/{itemId}");
            return Ok();
        }
        
        var i = await GetItem(itemId);
        
        if (i == null)
        {
            return NotFound("Item not found");
        }
        
        if (i.Quantity < q.Quantity)
        {
            return BadRequest("Item does not have enough quantity");
        }
        
        var json = JsonConvert.SerializeObject(q);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        await client.PutAsync($"{CartUrl}/cart/{userId}/{itemId}",data);
        return Ok();
        
    }
    
    [HttpDelete("{userId}/{itemId}")]
    public async Task<IActionResult> DeleteItemFromCart(string userId, string itemId)
    {
        if (!await CheckUser(userId))
        {
            return Unauthorized();
        }
        var i = await GetItem(itemId);
        
        if (i == null)
        {
            return NotFound("Item not found");
        }

        using var client = new HttpClient();
        
        await client.DeleteAsync($"{CartUrl}/cart/{userId}/{itemId}");
        return Ok();
        
    }
    private async Task<bool> CheckUser(string userId)
    {
        using var client = new HttpClient();
        var result = await client.GetAsync($"{AuthUrl}/auth/check/{userId}");
        var responseContent = await result.Content.ReadAsStringAsync();
        if (int.TryParse(responseContent, out int val))
        {
            if (val == 1)
            {
                return true;
            }

            return false;
        }

        return false;
        
    }
    
    private async Task<ItemViewModel> GetItem(string itemId)
    {

        using var client = new HttpClient();
        var getItemResult = await client.GetAsync($"{InventoryUrl}/inventory/{itemId}");
        var responseContent = await getItemResult.Content.ReadAsStringAsync();
        var items = JsonConvert.DeserializeObject<IEnumerable<ItemViewModel>>(responseContent);
        if (items == null)
        {
            return null;
        }

        if (items.Count() == 0)
        {
            return null;
        }

        return items.First();
        
    }

}