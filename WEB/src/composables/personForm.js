// A blank Person create/edit form — one canonical shape reused by the People list drawer,
// the person detail page, and the quick-add dialog on the user form.
export const blankPersonForm = () => ({
  tenantId: null,
  // The title they are addressed by — asked beside the first name, stored apart from it.
  prefix: "",
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
