import client from './client';

export const reportsApi = {
  getDailyReport: async () => {
    const { data } = await client.get('/reports/daily');
    return data;
  },

  runScrapersNow: async () => {
    const { data } = await client.post('/scrapers/run-now');
    return data;
  },
};
