namespace Deucalion.Application.Configuration;

public class ConfigurationErrorException : Exception
{
    public ConfigurationErrorException()
    {
    }

    public ConfigurationErrorException(string message)
        : base(message)
    {
    }

    public ConfigurationErrorException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
