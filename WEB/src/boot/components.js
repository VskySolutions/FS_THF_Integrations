import { boot } from "quasar/wrappers";
import ZwDate from "components/zw_date.vue";
import ZwCurrencyInput from "components/zw_currency_input.vue";
import ZwNumericInput from "components/zw_numeric_input.vue";

export default boot(({ app }) => {
  app.component("ZwDate", ZwDate);
  app.component("ZwCurrency", ZwCurrencyInput);
  app.component("ZwNumeric", ZwNumericInput);
});
