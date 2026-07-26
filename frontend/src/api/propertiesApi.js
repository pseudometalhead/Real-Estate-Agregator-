import client from './client';

export const propertiesApi = {
  getProperties: async (filters) => {
    const { data } = await client.get('/properties', { params: filters });
    return data;
  },

  getProperty: async (id) => {
    const { data } = await client.get(`/properties/${id}`);
    return data;
  },
};
