import client from './client';

export const myListingsApi = {
  getMyListings: async (filters) => {
    const { data } = await client.get('/mylistings', { params: filters });
    return data;
  },

  addToMyListings: async (payload) => {
    const { data } = await client.post('/mylistings', payload);
    return data;
  },

  updateMyListing: async (id, payload) => {
    const { data } = await client.put(`/mylistings/${id}`, payload);
    return data;
  },

  deleteMyListing: async (id) => {
    await client.delete(`/mylistings/${id}`);
  },
};
