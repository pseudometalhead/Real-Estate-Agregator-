import client from './client';

export const appSettingsApi = {
  getSettings: async () => {
    const { data } = await client.get('/appsettings');
    return data;
  },

  updateSettings: async (payload) => {
    const { data } = await client.put('/appsettings', payload);
    return data;
  },
};
