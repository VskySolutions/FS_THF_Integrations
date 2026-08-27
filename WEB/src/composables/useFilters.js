import { date } from "quasar";

export default function useFilters () {
  function toCurrency (value, precisons) {
    if (value !== undefined && value !== null) {
      const formatter = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", minimumFractionDigits: precisons, maximumFractionDigits: precisons });
      return formatter.format(value);
    }
    return value;
  }

  function toNumeric (value, precisons) {
    if (value !== undefined && value !== null) {
      const formatter = new Intl.NumberFormat("en-US", { minimumFractionDigits: precisons, maximumFractionDigits: precisons });
      return formatter.format(value);
    }
    return value;
  }

  function toPrice (value, precisons) {
    if (value !== undefined && value !== null) {
      value = value > 9 ? value : "0" + value;
      const newVal = parseFloat(value).toFixed(2);
      return newVal > 9 ? newVal : 0 + newVal;
    }
    return value;
  }

  function toPercentage (value, precisons) {
    if (value !== undefined && value !== null) {
      const formatter = new Intl.NumberFormat("en-US", { minimumFractionDigits: precisons, maximumFractionDigits: precisons });
      return formatter.format(value) + "%";
    }
    return value;
  }

  function toPhone (value, countryCode = "") {
    if (countryCode === "IN") {
      return value ? "+91" + " " + value : "";
    } else {
      return value ? "+1" + " " + value.replace(/(\d{3})(\d{3})(\d{4})/, "($1)$2-$3") : "";
    }
  }

  function toFax (value) {
    return value ? value.replace(/(\d{3})(\d{3})(\d{4})/, "($1)$2-$3") : "";
  }

  function toName (lastName, firstName, middleName) {
    const name = (lastName ?? "") + ", " + (firstName ?? "") + " " + (middleName ?? "");
    return name.trim();
  }

  function toDate (value, format) {
    return value ? date.formatDate(value, format || "MM/DD/YYYY") : "";
  }

  function toMonthYear (value, precisons) {
    // var targetMonthFormat = toDate(value, "YYYY/DD/MM");
    const targetMonthFormat = toDate(value, "MM/DD/YYYY");
    const targetMonthDate = new Date(targetMonthFormat);
    const month = targetMonthDate.toLocaleString("en-us", { month: "long" });
    return month + "-" + targetMonthDate.getFullYear();
  }

  function toDateTime (value, format) {
    return value ? date.formatDate(value, format || "MM/DD/YYYY hh:mm A") : "";
  }

  function truncate (value, length) {
    length = length || 15;
    if (!value || typeof value !== "string") return "";
    if (value.length <= length) return value;
    return value.substring(0, length) + "...";
  }

  function toLowercase (value) {
    return (value || value === 0) ? value.toString().toLowerCase() : "";
  }

  function toUpperCase (value) {
    return (value || value === 0) ? value.toString().toUpperCase() : "";
  }

  function stripHTML (str) {
    return str.replace(/(<([^>]+)>)/ig, "");
  }

  function randomIntFromInterval (min, max) { // min and max included
    return Math.floor(Math.random() * (max - min + 1) + min);
  }

  return {
    toCurrency,
    toNumeric,
    toPrice,
    toPercentage,
    toPhone,
    toFax,
    toName,
    toDate,
    toDateTime,
    toMonthYear,
    truncate,
    toLowercase,
    toUpperCase,
    stripHTML,
    randomIntFromInterval
  };
}
