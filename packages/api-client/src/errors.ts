export class ApiClientConfigurationError extends Error {
  public constructor(message: string) {
    super(message);
    this.name = "ApiClientConfigurationError";
  }
}

export class ApiHttpError extends Error {
  public readonly status: number;

  public constructor(status: number) {
    super(`API request failed with HTTP status ${status}.`);
    this.name = "ApiHttpError";
    this.status = status;
  }
}

export class ApiProblemError extends Error {
  public readonly status: number;
  public readonly code: string | undefined;
  public readonly title: string;

  public constructor(status: number, code: string | undefined, title: string) {
    super(`${title} (HTTP ${status})`);
    this.name = "ApiProblemError";
    this.status = status;
    this.code = code;
    this.title = title;
  }
}

export class ApiResponseParseError extends Error {
  public readonly status: number;

  public constructor(status: number) {
    super(`API response for HTTP status ${status} was not valid JSON.`);
    this.name = "ApiResponseParseError";
    this.status = status;
  }
}

export class ApiResponseTooLargeError extends Error {
  public readonly status: number;
  public readonly maximumBytes: number;

  public constructor(status: number, maximumBytes: number) {
    super(`API response exceeded the ${maximumBytes}-byte limit.`);
    this.name = "ApiResponseTooLargeError";
    this.status = status;
    this.maximumBytes = maximumBytes;
  }
}

export class ApiRequestAbortedError extends Error {
  public constructor() {
    super("API request was cancelled.");
    this.name = "ApiRequestAbortedError";
  }
}

export class ApiNetworkError extends Error {
  public constructor() {
    super("API request could not be completed.");
    this.name = "ApiNetworkError";
  }
}
