using TGF.CA.Infrastructure.Communication.Consumer.Manager;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TGF.CA.Infrastructure.Communication.Consumer.Host;
//CODE FROM https://github.com/ElectNewt/Distribt
public class ConsumerController<TMessage> : ControllerBase
{
    private readonly IConsumerManager<TMessage> _consumerManager;

    public ConsumerController(IConsumerManager<TMessage> consumerManager)
    {
        _consumerManager = consumerManager;
    }
    
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("start")]
    public virtual IActionResult Start()
    {
        _consumerManager.RestartExecution();
        return Ok();
    }
}