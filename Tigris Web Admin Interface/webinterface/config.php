<?php

// Connection's Parameters
$db_host="localhost";
$db_name="dbname";
$db_username="dbusername";
$db_password="password";

mysql_connect($db_host, $db_username, $db_password);
mysql_select_db($db_name);

?>