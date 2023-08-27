import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import duration from "dayjs/plugin/duration";
import relativeTime from "dayjs/plugin/relativeTime";

// --- Configuration

dayjs.extend(utc);
dayjs.extend(duration);
dayjs.extend(relativeTime);

export const dateTimeToString = (value: number) => dayjs.unix(value).format("YYYY-MM-DD HH:mm:ss");

export const dateTimeFromNow = (value: number, withoutSuffix?: boolean) => dayjs.unix(value).fromNow(withoutSuffix);
