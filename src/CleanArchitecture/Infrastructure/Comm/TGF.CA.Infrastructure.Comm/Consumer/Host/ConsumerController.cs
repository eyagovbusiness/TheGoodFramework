using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TGF.CA.Infrastructure.Comm.Consumer.Manager;

namespace TGF.CA.Infrastructure.Comm.Consumer.Host;

public class ConsumerController<TMessage> : ControllerBase {
    private readonly IConsumerManager<TMessage> _consumerManager;

    public ConsumerController(IConsumerManager<TMessage> consumerManager) {
        _consumerManager = consumerManager;
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Route("start")]
    public virtual IActionResult Start() {
        _consumerManager.RestartExecution();
        return Ok();
    }
}