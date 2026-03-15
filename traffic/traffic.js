import http from 'k6/http';
import { sleep } from 'k6';

export const options = {
  vus: Number(__ENV.VUS || 10),
  duration: __ENV.DURATION || '5m',
};

const baseUrl = __ENV.BASE_URL || 'http://localhost:8080';

export default function () {
  const id = Math.floor(Math.random() * 100000);
  http.get(`${baseUrl}/orders/${id}`);
  sleep(Number(__ENV.SLEEP_SECONDS || 0.5));
}
