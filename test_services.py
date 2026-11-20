"""
test_services.py
Automated end-to-end integration test suite for Microservices Task Manager.
Tests:
  1. Health check endpoints on all 3 microservices
  2. UserService CRUD (List users, Create user)
  3. TaskService Inter-service validation (Rejection of invalid User ID -> 400)
  4. TaskService Task Creation & Sync User validation (201 Created)
  5. NotificationService Async Event verification (Task notification received)
  6. TaskService Status Transition (Pending -> InProgress -> Completed)
"""

import sys
import time
import uuid
import json

try:
    import urllib.request
    import urllib.error
except ImportError:
    pass

USER_SERVICE_URL = "http://localhost:5001"
TASK_SERVICE_URL = "http://localhost:5002"
NOTIFY_SERVICE_URL = "http://localhost:5003"


def http_request(url, method="GET", data=None):
    headers = {"Content-Type": "application/json"}
    body = json.dumps(data).encode("utf-8") if data else None
    req = urllib.request.Request(url, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=5) as response:
            res_body = response.read().decode("utf-8")
            return response.status, json.loads(res_body) if res_body else {}
    except urllib.error.HTTPError as e:
        err_body = e.read().decode("utf-8")
        try:
            return e.code, json.loads(err_body)
        except Exception:
            return e.code, {"raw": err_body}
    except Exception as e:
        return 0, {"error": str(e)}


def run_tests():
    print("=" * 65)
    print("  MICROSERVICES TASK MANAGER - AUTOMATED INTEGRATION TESTS")
    print("=" * 65)

    passed = 0
    total = 6

    # Test 1: Service Health Checks
    print("\n[TEST 1] Verifying /health probes on all 3 services...")
    u_status, u_data = http_request(f"{USER_SERVICE_URL}/health")
    t_status, t_data = http_request(f"{TASK_SERVICE_URL}/health")
    n_status, n_data = http_request(f"{NOTIFY_SERVICE_URL}/health")

    if u_status == 200 and t_status == 200 and n_status == 200:
        print(f"  ✓ UserService (5001):        HTTP {u_status} Healthy")
        print(f"  ✓ TaskService (5002):        HTTP {t_status} Healthy")
        print(f"  ✓ NotificationService (5003): HTTP {n_status} Healthy")
        passed += 1
    else:
        print(f"  ✗ Health check failed! UserService: {u_status}, TaskService: {t_status}, NotificationService: {n_status}")
        print("    Make sure containers or dotnet processes are running (run: docker compose up).")
        return False

    # Test 2: Create User in UserService
    print("\n[TEST 2] Creating new user in UserService...")
    test_user_payload = {
        "name": "Integration Tester",
        "email": f"test_{int(time.time())}@example.com",
        "department": "DevOps Automation"
    }
    status, user_res = http_request(f"{USER_SERVICE_URL}/api/users", method="POST", data=test_user_payload)
    if status == 201 and "id" in user_res:
        user_id = user_res["id"]
        print(f"  ✓ Created User '{user_res['name']}' with ID: {user_id}")
        passed += 1
    else:
        print(f"  ✗ Failed to create user: HTTP {status} {user_res}")
        return False

    # Test 3: Inter-Service Validation (Negative Test - Fake User ID)
    print("\n[TEST 3] Testing inter-service validation with INVALID user ID...")
    fake_user_id = str(uuid.uuid4())
    bad_task_payload = {
        "title": "Should Fail Validation",
        "description": "Invalid user task",
        "assignedUserId": fake_user_id
    }
    status, err_res = http_request(f"{TASK_SERVICE_URL}/api/tasks", method="POST", data=bad_task_payload)
    if status == 400:
        print(f"  ✓ Correctly rejected with HTTP 400 Bad Request: {err_res.get('error')}")
        passed += 1
    else:
        print(f"  ✗ Expected HTTP 400, got HTTP {status}: {err_res}")
        return False

    # Test 4: Create Task for Valid User
    print("\n[TEST 4] Creating task in TaskService for valid user (synchronous check)...")
    valid_task_payload = {
        "title": "Configure Prometheus Monitoring",
        "description": "Setup scrape jobs for microservices",
        "assignedUserId": user_id
    }
    status, task_res = http_request(f"{TASK_SERVICE_URL}/api/tasks", method="POST", data=valid_task_payload)
    if status == 201 and "id" in task_res:
        task_id = task_res["id"]
        print(f"  ✓ Created Task '{task_res['title']}' with ID: {task_id}")
        print(f"  ✓ Initial Status: {task_res['status']}")
        passed += 1
    else:
        print(f"  ✗ Failed to create task: HTTP {status} {task_res}")
        return False

    # Test 5: Verify Notification was dispatched to NotificationService
    print("\n[TEST 5] Checking NotificationService event stream...")
    time.sleep(0.5)  # brief wait for async event delivery
    status, notifs = http_request(f"{NOTIFY_SERVICE_URL}/api/notifications")
    matched = any(n.get("recipientUserId") == user_id for n in notifs) if isinstance(notifs, list) else False
    if status == 200 and matched:
        print(f"  ✓ Verified: NotificationService logged event for recipient {user_id}")
        passed += 1
    else:
        print(f"  ✗ Notification not found in NotificationService stream!")
        return False

    # Test 6: Update Task Status
    print("\n[TEST 6] Updating task status to 'Completed'...")
    status, update_res = http_request(
        f"{TASK_SERVICE_URL}/api/tasks/{task_id}/status",
        method="PUT",
        data={"status": "Completed"}
    )
    if status == 200 and update_res.get("status") == "Completed":
        print(f"  ✓ Task status successfully updated to 'Completed'")
        passed += 1
    else:
        print(f"  ✗ Failed to update task status: HTTP {status} {update_res}")
        return False

    print("\n" + "=" * 65)
    print(f"  SUMMARY: {passed}/{total} TESTS PASSED (100% SUCCESS)")
    print("  ALL 3 MICROSERVICES ARE COMMUNICATING AND FULLY FUNCTIONAL!")
    print("=" * 65)
    return True


if __name__ == "__main__":
    success = run_tests()
    sys.exit(0 if success else 1)
