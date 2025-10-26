namespace CarbonTracker.API.Tests.Controllers;

using CarbonTracker.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Data;

public class ItemsControllerTests
{
    [Fact]
    public void AddItem_ReturnsBadRequest_WhenItemIsNull()
    {
        var mockDb = new Mock<IDbConnection>();
        var controller = new ItemsController(mockDb.Object);

        var result = controller.AddItem(null);

        Assert.IsType<BadRequestResult>(result);
    }
}