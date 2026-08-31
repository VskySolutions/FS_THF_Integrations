// A blank Person create/edit form — one canonical shape reused by the People list drawer,
// the person detail page, and the quick-add dialog on the user form.
export const blankPersonForm = () => ({
  tenantId: null,
  // The generational particle on their name — asked beside the name, stored apart from it.
  suffix: "",
  firstName: "",
  middleName: "",
  lastName: "",
  preferredName: "",
  gender: null,
  dateOfBirth: "",
  primaryEmail: "",
  mobileNumber: "",
  countryCode: null
});
