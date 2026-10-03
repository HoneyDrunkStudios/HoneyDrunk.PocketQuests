export class RequestError extends Error {
  readonly status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}
// These statuses confirm rejection. Authentication failures, rate limits,
// timeouts and server errors retain the original operation ID for safe retry.
export function isDefinitiveRejection(error: unknown): boolean {
  return (
    error instanceof RequestError && [400, 404, 409, 422].includes(error.status)
  );
}
