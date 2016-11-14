<?
include("config.php"); 
include("payload.php"); 

function ascii_to_dec($str)
{
  for ($i = 0, $j = strlen($str); $i < $j; $i++) {
    $dec_array[] = ord($str{$i});
  }
  return $dec_array;
}

// a = hardware id
// b = username
// c = password
// d = action
if(isset($_GET['a']) && isset($_GET['b']) && isset($_GET['c']) && isset($_GET['d']))
{
	// First let's verify does this user exist
	
	// Get his data
	$hwid = mysql_real_escape_string($_GET['a']);	
	$username = mysql_real_escape_string($_GET['b']);
	$password = mysql_real_escape_string($_GET['c']);	
	$action = mysql_real_escape_string($_GET['d']);
	
	// Get the real hardware ID that is stored in the database
	$rnd1 = substr($hwid, 0, 2);
	$rnd2 = substr($hwid, strlen($hwid) - 3, 3);
	$hwid = substr($hwid, 2);
	$hwid = substr($hwid, 0, strlen($hwid) - 3);
	
	$query = mysql_query("SELECT * FROM tigris_accounts WHERE username='$username' AND password='$password' AND hw_id='$hwid' AND allowed=1");
	
	if(mysql_num_rows($query) == 1)
	{
		// User exists, now do w/e it wants
		if($action == "init")
		{
			$rnd = $rnd1 + $rnd2;
			$this_rnd = rand(11, 1111);		
			$enc = $this_rnd . " ";
			
			$dec_payload = ascii_to_dec($payload);	// Here's the payload
			$dec_payload_len = count($dec_payload);
			$dec_hwid = ascii_to_dec($hwid);
			$dec_hwid_len = count($dec_hwid);
			
			for($i = 0, $j = 0; $i < $dec_payload_len; $i++)
			{
				$char = $dec_payload[$i] ^ $rnd;
				$char = $char ^ $this_rnd ^ $dec_hwid[$j];
				$enc = $enc . $char . " ";
				$j++;
				if($j >= $dec_hwid_len) $j = 0;
			}
			
			// End format:
			// [thisrnd] [payload] [rnd]
			echo "gjmate" . $enc . $rnd;	
			
			// Update last use and action
			$ip = $_SERVER['REMOTE_ADDR'];
			mysql_query("UPDATE tigris_accounts SET last_used=NOW()	WHERE username='$username'");
			mysql_query("INSERT INTO tigris_usages (username, action, ip, hw_id, usage_time)
VALUES ('$username', 'Initialize', '$ip', '$hwid', NOW())");
			exit();
		}
		
		if($action == "start")
		{
			// Update last use and action
			$ip = $_SERVER['REMOTE_ADDR'];
			$usage_text = "Started " . mysql_real_escape_string($_GET['e']) . " citizens";
			mysql_query("UPDATE tigris_accounts SET last_used=NOW()	WHERE username='$username'");
			mysql_query("INSERT INTO tigris_usages (username, action, ip, hw_id, usage_time)
VALUES ('$username', '$usage_text', '$ip', '$hwid', NOW())");
		}
		if($action == "reg")
		{
			// Update last use and action
			$ip = $_SERVER['REMOTE_ADDR'];
			$usage_text = "Registering " . mysql_real_escape_string($_GET['e']) . " citizens";
			mysql_query("UPDATE tigris_accounts SET last_used=NOW()	WHERE username='$username'");
			mysql_query("INSERT INTO tigris_usages (username, action, ip, hw_id, usage_time)
VALUES ('$username', '$usage_text', '$ip', '$hwid', NOW())");
		}
	}
	else
	{
		$ip = $_SERVER['REMOTE_ADDR'];
		$usage_text = "S: Unsuccessful login.";
		mysql_query("INSERT INTO tigris_usages (username, action, ip, hw_id, usage_time)
VALUES ('$username', '$usage_text', '$ip', '$hwid', NOW())");
	}
}
else
{
	// A bug report?
	if(isset($_GET['bug']))
	{
		$bug = mysql_real_escape_string($_GET['bug']);
		$message = mysql_real_escape_string($_GET['message']);
		$stack = mysql_real_escape_string($_GET['stack']);
		$citizen = mysql_real_escape_string($_GET['citizen']);
		
		mysql_query("INSERT INTO tigris_bugs (bug, message, details, user, citizen)
VALUES ('$bug', '$message', '$stack', 'n/a', '$citizen')");
		echo "thanks mate!";
	}
	
	// Request
	if(isset($_GET['req']))
	{
		$username = mysql_real_escape_string($_GET['u']);
		$password = mysql_real_escape_string($_GET['p']);
		$ip = $_SERVER['REMOTE_ADDR'];
		
		// Get the HW ID to store in the database
		$hwid = base64_decode(mysql_real_escape_string($_GET['h']));
		$rnd1 = substr($hwid, 0, 2);
		$rnd2 = substr($hwid, strlen($hwid) - 3, 3);
		$hwid = substr($hwid, 2);
		$hwid = substr($hwid, 0, strlen($hwid) - 3);
		
		mysql_query("INSERT INTO tigris_accounts (username, password, last_ip, first_ip, hw_id, last_used, allowed)
		VALUES ('$username', '$password', '$ip', '$ip', '$hwid', 0, 0)");
	}
}

echo "wrongc";
?>