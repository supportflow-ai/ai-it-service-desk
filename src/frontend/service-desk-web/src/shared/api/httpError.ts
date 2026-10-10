import { isAxiosError } from 'axios';

/** Read only HTTP status; arbitrary caught values are not HTTP responses. */
export function getHttpErrorStatus(error: unknown): number | undefined {
  return isAxiosError(error) ? error.response?.status : undefined;
}
