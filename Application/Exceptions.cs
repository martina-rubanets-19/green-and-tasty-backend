namespace GroupPreOrder.Application;

/// <summary>Ресурс не знайдено → 404</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Некоректний запит / порушення бізнес-правила → 400</summary>
public class BadRequestException(string message) : Exception(message);

/// <summary>Користувач не є Owner → 403</summary>
public class ForbiddenException(string message) : Exception(message);
