import { helpers } from "@vuelidate/validators";
import { isValid, isBefore, isAfter, isEqual } from "date-fns";

export const isDate = function (value) {
  const date = new Date(value);
  const min = new Date("01/01/1753");
  const max = new Date("12/31/9999");
  return value ? (isValid(date) && value.length === 10 && (isAfter(date, min) || isEqual(date, min)) &&
  (isBefore(date, max) || isEqual(date, max))) : true;
};

export const isLessThan = function (value1, value2) {
  return (value1 && value2) ? isBefore(new Date(value1), new Date(value2)) : true;
};

export const isLessThanOrEqual = function (value1, value2) {
  const date1 = new Date(value1);
  const date2 = new Date(value2);
  return (value1 && value2) ? (isBefore(date1, date2) || isEqual(date1, date2)) : true;
};

export const isLessThanToday = function (value) {
  const date = new Date(value);
  const now = new Date().setHours(0, 0, 0, 0);
  return value ? isBefore(date, now) : true;
};

export const isGreaterThan = function (value1, value2) {
  return (value1 && value2) ? isAfter(new Date(value1), new Date(value2)) : true;
};

export const isGreaterThanOrEqual = function (value1, value2) {
  const date1 = new Date(value1);
  const date2 = new Date(value2);
  return (value1 && value2) ? (isAfter(date1, date2) || isEqual(date1, date2)) : true;
};

export const isGreaterThanToday = function (value) {
  const date = new Date(value);
  const now = new Date().setHours(0, 0, 0, 0);
  return value ? isAfter(date, now) : true;
};

export const isDayEqual = function (value, day) {
  const date = new Date(value);
  return (value && day) ? (date.getDate() === day) : true;
};

export const name = helpers.regex(/^[^~!@#$%^&*0-9]*$/);

export const password1 = helpers.regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[~@!#$%^&*])[A-Za-z\d~!@#$%^&*]{4,}$/);

export const password2 = helpers.regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d]{4,}$/);

export const email = helpers.regex(/^\w+([.-]?\w+)*@\w+([.-]?\w+)*(\.\w{2,3})+$/);
