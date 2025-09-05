using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using CarbonTracker.API.Controllers;
using CarbonTracker.API.Models;
using Microsoft.AspNetCore.Mvc;

public class ItemsControllerTests
{
    [Fact]
    public void AddItem_ReturnsBadRequest_WhenItemIsNull()
    {
        var mockConfig = new Mock<IConfiguration>();
        var controller = new ItemsController(mockConfig.Object);

        var result = controller.AddItem(null);

        Assert.IsType<BadRequestResult>(result);
    }
}