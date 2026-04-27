namespace WebAPI.Common;

public class BusinessException : Exception
{
    public BusinessException(string message, int code) : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}
