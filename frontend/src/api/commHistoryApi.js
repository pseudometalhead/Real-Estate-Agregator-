import client from './client';

export const commHistoryApi = {
  getHistory: async (myListingId) => {
    const { data } = await client.get(`/mylistings/${myListingId}/comms`);
    return data;
  },

  addEntry: async (myListingId, payload) => {
    const { data } = await client.post(`/mylistings/${myListingId}/comms`, payload);
    return data;
  },

  deleteEntry: async (myListingId, commId) => {
    await client.delete(`/mylistings/${myListingId}/comms/${commId}`);
  },
};
