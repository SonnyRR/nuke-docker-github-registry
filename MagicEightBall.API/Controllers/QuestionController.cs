using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MagicEightBall.API.Controllers;

/// <summary>
/// A question answering API controller.
/// </summary>
[ApiController]
[Route("/api/[controller]")]
public class QuestionController : ControllerBase
{
    private static readonly string[] PossibleAnswers = new[] 
    {
        "As I see it, yes.",
        "Ask again later.",
        "Better not tell you now.",
        "Cannot predict now.",
        "Concentrate and ask again.",
        "Don't count on it.",
        "It is certain.",
        "It is decidedly so."
    };

    private readonly Random _randomGenerator;
    
    /// <summary>
    /// Creates a new instance of a question answering API controller.
    /// </summary>
    public QuestionController()
    {
        _randomGenerator = new Random();
    }

    /// <summary>
    /// Answers a question provided by the user.
    /// </summary>
    /// <param name="question" example="Елена може ли да прави сарми ?">
    /// The question itself.
    /// </param>
    /// <returns>An <see cref="string">answer</see>.</returns>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Get([FromQuery(Name = "q")] string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return BadRequest();

        return Ok(PossibleAnswers[_randomGenerator.Next(PossibleAnswers.Length - 1)]);
    }
}